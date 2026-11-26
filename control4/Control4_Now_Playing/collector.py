"""Read-only LAN collector for the Control4 Now Playing driver."""
import argparse
import asyncio
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from ipaddress import IPv4Address
import json
import re
import subprocess
from pathlib import Path
from threading import Event, Lock, Thread
import time
from urllib.request import build_opener, ProxyHandler, HTTPRedirectHandler
import xml.etree.ElementTree as ET


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def roku(host):
    client = build_opener(ProxyHandler({}), NoRedirect())
    def query(path):
        with client.open(f'http://{host}:8060/query/{path}', timeout=3) as response:
            return ET.fromstring(response.read(32768))
    app = query('active-app').findtext('app')
    try:
        player = query('media-player')
    except OSError:
        return {'app': app}
    def seconds(field):
        raw = re.fullmatch(r'(\d+) ms', player.findtext(field) or '')
        return int(raw[1]) / 1000 if raw else None
    return dict(app=app, state=player.get('state'), title=player.findtext('.//title'),
                position=seconds('position'), duration=seconds('duration'))


def cast_status(host):
    from pychromecast import get_chromecast_from_host
    cast = get_chromecast_from_host((host, 8009, None, None, host), tries=1, timeout=3)
    try:
        cast.wait(timeout=5)
        receiver = cast.status
        media = cast.media_controller
        if 'urn:x-cast:com.google.cast.media' not in (receiver.namespaces or []):
            return {'app': receiver.display_name}
        reply = Event()
        media.update_status(callback_function_param=lambda *args: reply.set())
        if not reply.wait(3):
            return {'app': receiver.display_name}
        status = media.status
        return dict(app=receiver.display_name, title=status.title,
                    subtitle=(status.media_metadata or {}).get('artist'),
                    state=status.player_state, position=status.current_time,
                    duration=status.duration)
    finally:
        cast.disconnect(timeout=3)


async def apple(host, credentials):
    import pyatv
    from pyatv.const import Protocol
    loop = asyncio.get_running_loop()
    devices = await pyatv.scan(loop, hosts=[host], timeout=3)
    if len(devices) != 1:
        return {}
    device = devices[0]
    device.set_credentials(Protocol.AirPlay, Path(credentials).read_text().strip())
    tv = await asyncio.wait_for(pyatv.connect(device, loop), 6)
    try:
        p = await asyncio.wait_for(tv.metadata.playing(), 4)
        try:
            app = tv.metadata.app
        except pyatv.exceptions.NotSupportedError:
            app = None
        return dict(app=getattr(app, 'name', None), title=p.title, subtitle=p.series_name or p.artist,
                    state=p.device_state.name, position=p.position, duration=p.total_time)
    finally:
        await asyncio.gather(*tv.close(), return_exceptions=True)


def shield(host, adb):
    target = f'{host}:5555'
    subprocess.run([adb, 'connect', target], capture_output=True, timeout=4)
    def dump(*args):
        return subprocess.run([adb, '-s', target, 'shell', 'dumpsys', *args],
                              check=True, timeout=4, capture_output=True, text=True,
                              encoding='utf-8', errors='replace').stdout
    focus = re.search(r'(?:mResumedActivity|topResumedActivity):.*?\s([a-zA-Z0-9_.]+)/', dump('activity', 'activities'))
    if not focus:
        return {}
    app_id = focus[1]
    labels = {'com.netflix.ninja': 'Netflix', 'com.disney.disneyplus': 'Disney+',
              'com.google.android.youtube.tv': 'YouTube', 'com.att.tv': 'DIRECTV Stream',
              'com.cbs.ott': 'Paramount+'}
    if app_id.endswith('.launcher') or '.launcher.' in app_id:
        return {}
    result = {'app': labels.get(app_id, app_id)}
    sessions = re.split(r'(?m)^\s*package=', dump('media_session'))
    matches = [body for body in sessions[1:] if body.splitlines()[0].strip() == app_id
               and re.search(r'(?m)^\s*active=true\s*$', body)]
    if len(matches) != 1:
        return result
    body = matches[0]
    state = re.search(r'PlaybackState \{state=(\d+), position=(-?\d+)', body)
    if state:
        result['state'] = {'2':'paused', '3':'playing', '6':'buffering'}.get(state[1])
        result['position'] = int(state[2]) / 1000
    desc = re.search(r'(?m)^\s*metadata: .*?description=([^\r\n]+)', body)
    if desc:
        parts = [part.strip() for part in desc[1].rsplit(',', 2)]
        if len(parts) == 3 and app_id in labels:
            for key, part in zip(('title', 'subtitle', 'description'), parts):
                if part and part != 'null':
                    result[key] = part
    return result


def read_device(device):
    kind, host = device['kind'], device['host']
    try:
        if kind == 'roku':
            data = roku(host)
        elif kind == 'cast':
            data = cast_status(host)
        elif kind == 'apple':
            data = asyncio.run(apple(host, device['credentials_file']))
        else:
            data = shield(host, device.get('adb', 'adb'))
    except Exception as exc:
        print(f'{kind} {host}: {type(exc).__name__}', flush=True)
        return None
    normalized = {}
    for key in ('app', 'title', 'subtitle', 'description'):
        value = data.get(key)
        if isinstance(value, str) and value.strip():
            normalized[key] = value.strip()[:200]
    state = str(data.get('state') or '').lower()
    state = {'play': 'playing', 'pause': 'paused', 'buffer': 'buffering', 'idle': 'idle'}.get(state, state)
    if state in {'playing', 'paused', 'buffering', 'idle'}:
        normalized['state'] = state
    duration = data.get('duration')
    position = data.get('position')
    if isinstance(duration, (int, float)) and 0 < duration <= 86400:
        normalized['duration'] = int(duration)
        if isinstance(position, (int, float)) and 0 <= position <= duration:
            normalized['position'] = int(position)
    return normalized or None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('config', type=Path)
    args = parser.parse_args()
    config = json.loads(args.config.read_text(encoding='utf-8-sig'))
    bind = str(IPv4Address(config['listen_host']))
    director = str(IPv4Address(config['director_host']))
    devices = config['devices']
    if not 1 <= len(devices) <= 16:
        raise ValueError('Configure 1–16 devices')
    ids = set()
    for device in devices:
        if device['kind'] not in {'shield','cast','apple','roku'}:
            raise ValueError('Unknown reader kind')
        device['host'] = str(IPv4Address(device['host']))
        for source in device['sources']:
            if type(source) is not int or not 0 < source <= 2147483647 or source in ids:
                raise ValueError('Watch source IDs must be positive unique integers')
            ids.add(source)
    lock = Lock()
    samples = {}
    def poll(device):
        while True:
            data = read_device(device)
            with lock:
                for source in device['sources']:
                    samples[source] = (time.monotonic(), data)
            time.sleep(4)
    for device in devices:
        Thread(target=poll, args=(device,), daemon=True).start()
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.client_address[0] != director or self.path != '/now-playing':
                self.send_error(404)
                return
            with lock:
                rows = [dict(source_id=source, **value) for source, (stamp,value) in samples.items()
                        if value and time.monotonic() - stamp < 20]
            body = json.dumps({'version': 1, 'sources': rows}, ensure_ascii=True).encode('ascii')
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Cache-Control', 'no-store')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        def log_message(self, *args):
            pass
    with ThreadingHTTPServer((bind, int(config.get('listen_port', 48125))), Handler) as server:
        print(f'Now Playing collector listening on {bind}:{server.server_port}', flush=True)
        server.serve_forever()


if __name__ == '__main__':
    main()

