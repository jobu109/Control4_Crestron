import { describe, expect, it } from 'vitest';
import { FrameCollector } from '../src/adapters/wire';
import raw from './fixtures/csharp-frames.json?raw';

// Frames produced by the real Control4Gateway UiSerialFrames encoder (Release
// DLL, loaded by reflection). Regenerate with the script described in
// docs/PANEL_ROUTER_ADAPTER.md when the C# framing changes.
interface Vector { room: number; kind: string; json: string; frames: string[] }
const vectors: Vector[] = JSON.parse(raw);

describe('C# UiSerialFrames compatibility', () => {
  it.each(vectors.map((v) => [v.kind, v] as const))('decodes %s frames losslessly', (_kind, v) => {
    expect(v.frames.length).toBeGreaterThan(0);
    for (const f of v.frames) expect(f.length).toBeLessThanOrEqual(255);
    const collector = new FrameCollector(v.room, v.kind);
    let result;
    for (const f of v.frames) result = collector.accept(f);
    expect(result).toEqual({ status: 'complete', document: JSON.parse(v.json) });
  });

  it('recovers when the last page arrives first', () => {
    const v = vectors.find((x) => x.frames.length > 2)!;
    const collector = new FrameCollector(v.room, v.kind);
    expect(collector.accept(v.frames[v.frames.length - 1])).toEqual({ status: 'partial', requestPage: 1 });
    let result;
    for (const f of v.frames.slice(0, -1)) result = collector.accept(f);
    expect(result).toEqual({ status: 'complete', document: JSON.parse(v.json) });
  });
});
