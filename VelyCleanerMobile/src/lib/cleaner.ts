import { Directory, Paths } from 'expo-file-system';

export type CleanItem = {
  id: string;
  name: string;
  description: string;
  defaultOn: boolean;
  dir: Directory;
};

// En iOS, Apple solo deja a una app tocar su propio sandbox: no existe
// forma de limpiar caché de otras apps ni del sistema, como sí se puede
// en Windows/Mac. Esto es lo único real que hay para limpiar aquí.
export const CLEAN_ITEMS: CleanItem[] = [
  {
    id: 'cache',
    name: 'Caché de la app',
    description: 'Imágenes y datos temporales que guarda VELY157 Cleaner.',
    defaultOn: true,
    dir: Paths.cache,
  },
];

export function measure(item: CleanItem): number {
  try {
    if (!item.dir.exists) return 0;
    return item.dir.size ?? 0;
  } catch {
    return 0;
  }
}

export function clean(item: CleanItem): number {
  try {
    if (!item.dir.exists) return 0;
    const freed = item.dir.size ?? 0;
    for (const entry of item.dir.list()) {
      try {
        entry.delete();
      } catch {
        // seguimos con el resto
      }
    }
    return freed;
  } catch {
    return 0;
  }
}

export function formatBytes(bytes: number): string {
  if (bytes >= 1 << 30) return `${(bytes / (1 << 30)).toFixed(2)} GB`;
  if (bytes >= 1 << 20) return `${(bytes / (1 << 20)).toFixed(1)} MB`;
  if (bytes >= 1 << 10) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
}
