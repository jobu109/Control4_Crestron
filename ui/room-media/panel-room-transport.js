(function (global) {
  "use strict";

  const commandJoin = "101";
  const stateJoin = "102";
  let sequence = 0;

  function create(com, config) {
    const roomId = config.roomId;
    const send = config.send, receive = config.receive;
    const virtualSubscriptions = new Map();
    const physicalSubscriptions = [];
    let lastSource = "", active = false, connected = false, started = false, nextId = 0;

    function emit(type, join, value) {
      for (const item of virtualSubscriptions.values()) {
        if (item.type === type && item.join === join) item.callback(value);
      }
    }
    function watch(type, join, callback) {
      physicalSubscriptions.push({ type, join, id: com.subscribeState(type, join, callback) });
    }
    function request(op, fields) {
      if (!active || !connected) return;
      const json = JSON.stringify({ ...fields, op, room_id: roomId, q: ++sequence });
      if (json.length <= 255) com.publishEvent("s", commandJoin, json);
    }
    const pages = new Map([
      [send.audioPage, "audio_page"], [send.videoPage, "video_page"],
      [send.mediaPage, "media_page"], [send.nowPlayingPage, "playing_page"], [send.volume, "volume"]
    ]);
    const keys = new Map(Object.entries(send.transport).map(([key, join]) => [join, key]));
    const operations = new Map([
      [send.muteOn, "mute_on"], [send.muteOff, "mute_off"], [send.off, "off"]
    ]);
    const stateFields = [
      ["volume", "n", receive.volume], ["volumeValid", "b", receive.volumeValid],
      ["muted", "b", receive.muted], ["muteValid", "b", receive.sources.Mute_Valid_FB],
      ["power", "b", receive.power], ["sourceValid", "b", receive.sourceValid],
      ["mediaValid", "b", receive.mediaValid], ["selectReady", "b", receive.selectReady],
      ["nowPlayingValid", "b", receive.nowPlayingValid],
      ["audioIndex", "n", receive.sources.Audio_Source_Selected_FB],
      ["videoIndex", "n", receive.sources.Video_Source_Selected_FB],
      ["audioValid", "b", receive.sources.Audio_Source_Selected_Valid_FB],
      ["videoValid", "b", receive.sources.Video_Source_Selected_Valid_FB]
    ];
    let state = new Map();
    let deviceCatalog = [];
    const deviceValues = new Map();
    const deviceWire = new global.RoomSerialWire.Collector(roomId, "d");
    function publishDevices() {
      onDevices(deviceCatalog.map(device => ({ ...device, value: deviceValues.get(device.id) ?? null })));
    }
    let onDevices = () => {};
    const streams = [receive.audioSources, receive.videoSources, receive.mediaList, receive.nowPlaying, receive.progress, receive.transport];

    return {
      subscribeState(type, join, callback) {
        const id = ++nextId;
        virtualSubscriptions.set(id, { type, join: String(join), callback });
        return id;
      },
      unsubscribeState(type, join, id) { virtualSubscriptions.delete(id); },
      onDeviceFeedback(callback) { onDevices = callback; },
      sendDevice(device_id, action, value) {
        const device = deviceCatalog.find(item => item.id === device_id);
        if (!device || (action !== "level" && (!device.shade || !["open", "close", "stop"].includes(action)))) return;
        if (action === "level" && (!Number.isInteger(value) || value < 0 || value > 100)) return;
        request("device", { device_id, action, value });
      },
      start() {
        if (started) return;
        started = active = true;
        watch("s", stateJoin, value => {
          let packet;
          try { packet = JSON.parse(value); } catch (_) { return; }
          if (!active || !packet || packet.r !== roomId) return;
          if (packet.k === "d") {
            let result;
            try { result = deviceWire.accept(packet); } catch (_) { return; }
            if (result?.requestPage) request("device_page", { value: result.requestPage });
            if (Array.isArray(result?.packet?.devices)) {
              deviceCatalog = result.packet.devices.filter(device => device && Number.isInteger(device.id) &&
                device.id > 0 && typeof device.name === "string" && typeof device.shade === "boolean");
              for (const device of deviceCatalog) if (!deviceValues.has(device.id)) deviceValues.set(device.id, device.value);
              publishDevices();
            }
            return;
          }
          if (Number.isInteger(packet.u) && packet.u > 0 && (packet.v === null ||
              (Number.isInteger(packet.v) && packet.v >= 0 && packet.v <= 100))) {
            deviceValues.set(packet.u, packet.v);
            if (deviceCatalog.some(device => device.id === packet.u)) publishDevices();
            return;
          }
          for (const [key, type, join] of stateFields) {
            if (!Object.hasOwn(packet, key)) continue;
            const value = type === "b" ? packet[key] === 1 : packet[key];
            if (state.has(key) && state.get(key) === value) continue;
            state.set(key, value);
            emit(type, join, value);
          }
        });
        streams.forEach((join, index) => watch("s", String(index + 103), value => {
          let packet;
          try { packet = JSON.parse(value); } catch (_) { return; }
          if (active && packet && (packet.r ?? packet.room_id) === roomId) emit("s", join, value);
        }));
        watch("b", receive.connected, value => {
          if (!active || connected === Boolean(value)) return;
          connected = Boolean(value);
          if (!connected) {
            state = new Map();
            deviceValues.clear();
            publishDevices();
          }
          emit("b", receive.connected, connected);
          if (connected) {
            deviceWire.reset();
            request("room");
            if (deviceWire.pending) request("device_page", { value: deviceWire.pending });
          }
        });
      },
      publishEvent(type, join, value) {
        if (type === "s" && join === send.sourceId) { lastSource = String(value); return; }
        if (type === "s" && join === send.mediaRequest) {
          try { request("item", JSON.parse(value)); } catch (_) {}
          return;
        }
        if (type === "n" && pages.has(join)) {
          if (value || join === send.volume) request(pages.get(join), { value });
          return;
        }
        if (type !== "b") return;
        if (join === send.shuffle) { request("shuffle", { value: value ? 1 : 0 }); return; }
        if (!value) return;
        if (join === send.selectAudio || join === send.selectVideo) {
          if (lastSource) request(join === send.selectAudio ? "audio" : "video", { value: lastSource });
        } else if (operations.has(join)) request(operations.get(join));
        else if (keys.has(join)) request("key", { value: keys.get(join) });
      },
      subscriptionCount() { return physicalSubscriptions.length; },
      destroy() {
        active = false;
        for (const item of physicalSubscriptions) com.unsubscribeState(item.type, item.join, item.id);
        physicalSubscriptions.length = 0;
        virtualSubscriptions.clear();
      }
    };
  }

  global.PanelRoomTransport = { create };
})(window);
