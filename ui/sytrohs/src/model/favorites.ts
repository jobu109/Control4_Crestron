import type { Command, HouseConfig } from './types';

export interface FavoriteAction {
  key: string;
  name: string;
  detail: string;
  command: Command;
}

/** Build actions from the current scoped catalog. Store identities, never an old revision or command. */
export function favoriteActions(config: HouseConfig): FavoriteAction[] {
  const actions: FavoriteAction[] = config.scenes.map((scene) => ({
    key: JSON.stringify(['scene', scene.description ?? '', scene.name]),
    name: scene.name,
    detail: scene.description ?? 'Scene',
    command: { kind: 'scene', id: scene.id, action: 'run' }
  }));

  for (const room of config.rooms) {
    for (const button of room.experienceButtons ?? []) {
      actions.push({
        key: JSON.stringify(['experience-button', room.id, room.name, button.id, button.name]),
        name: button.name,
        detail: room.name,
        command: { kind: 'experience-button', id: button.id, roomId: room.id, action: 'select' }
      });
    }
  }

  const counts = new Map<string, number>();
  for (const action of actions) counts.set(action.key, (counts.get(action.key) ?? 0) + 1);
  return actions.filter((action) => counts.get(action.key) === 1);
}

export function readFavoriteKeys(raw: string | null): string[] {
  try {
    const values: unknown = raw === null ? [] : JSON.parse(raw);
    if (!Array.isArray(values)) return [];
    return [...new Set(values.filter((key): key is string => typeof key === 'string'))];
  } catch {
    return [];
  }
}
