import { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { PressableScale } from '@/components/PressableScale';
import { CLEAN_ITEMS, clean, formatBytes, measure } from '@/lib/cleaner';
import { colors } from '@/lib/theme';

type Busy = 'idle' | 'analyzing' | 'cleaning';

export default function Home() {
  const [checked, setChecked] = useState<Record<string, boolean>>(
    Object.fromEntries(CLEAN_ITEMS.map((i) => [i.id, i.defaultOn])),
  );
  const [sizes, setSizes] = useState<Record<string, number>>({});
  const [busy, setBusy] = useState<Busy>('idle');
  const [freed, setFreed] = useState<number | null>(null);
  const [progress, setProgress] = useState(0);
  const [status, setStatus] = useState('Analizando tu iPhone…');

  function analyze() {
    setBusy('analyzing');
    setFreed(null);
    setStatus('Analizando tu iPhone…');
    const next: Record<string, number> = {};
    CLEAN_ITEMS.forEach((item, i) => {
      next[item.id] = measure(item);
      setProgress(((i + 1) / CLEAN_ITEMS.length) * 100);
    });
    setSizes(next);
    setProgress(0);
    setBusy('idle');
    const total = CLEAN_ITEMS.filter((i) => checked[i.id]).reduce((s, i) => s + (next[i.id] ?? 0), 0);
    setStatus(`Podés liberar unos ${formatBytes(total)}.`);
  }

  useEffect(() => {
    const id = setTimeout(analyze, 0);
    return () => clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function cleanSelected() {
    const selected = CLEAN_ITEMS.filter((i) => checked[i.id]);
    if (selected.length === 0) {
      setStatus('No marcaste nada para limpiar.');
      return;
    }
    setBusy('cleaning');
    let total = 0;
    const next = { ...sizes };
    selected.forEach((item, i) => {
      setStatus(`Limpiando: ${item.name}…`);
      const bytes = clean(item);
      total += bytes;
      next[item.id] = 0;
      setProgress(((i + 1) / selected.length) * 100);
    });
    setSizes(next);
    setProgress(0);
    setFreed(total);
    setStatus(`¡Listo! Se liberaron ${formatBytes(total)}.`);
    setBusy('idle');
  }

  const selectedTotal = CLEAN_ITEMS.filter((i) => checked[i.id]).reduce((s, i) => s + (sizes[i.id] ?? 0), 0);
  const bigText = freed !== null ? formatBytes(freed) : formatBytes(selectedTotal);
  const smallText = freed !== null ? 'liberado' : 'para liberar';
  const ringColor = freed !== null ? colors.green : colors.blue;
  const busyNow = busy !== 'idle';

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <View style={styles.header}>
          <Text style={styles.logo}>VELY157</Text>
          <Text style={styles.logoSub}>C L E A N E R</Text>
          <View style={styles.freeBadge}>
            <Text style={styles.freeBadgeText}>GRATIS en iPhone</Text>
          </View>
        </View>

        <View style={[styles.ring, { borderColor: ringColor }]}>
          <Text style={styles.ringBig}>{bigText}</Text>
          <Text style={styles.ringSmall}>{smallText}</Text>
        </View>

        <View style={styles.progressTrack}>
          <View style={[styles.progressFill, { width: `${progress}%` }]} />
        </View>
        <Text style={styles.status}>{status}</Text>

        <View style={styles.buttonsRow}>
          <PressableScale
            style={[styles.secondaryButton, busyNow && styles.disabled]}
            disabled={busyNow}
            onPress={analyze}
          >
            <Text style={styles.secondaryButtonText}>Analizar</Text>
          </PressableScale>
          <PressableScale
            style={[styles.primaryButton, busyNow && styles.disabled]}
            disabled={busyNow}
            onPress={cleanSelected}
          >
            <Text style={styles.primaryButtonText}>Limpiar  ✦</Text>
          </PressableScale>
        </View>

        <Text style={styles.sectionTitle}>¿Qué limpiar?</Text>
        {CLEAN_ITEMS.map((item) => (
          <PressableScale
            key={item.id}
            style={styles.itemRow}
            onPress={() => setChecked((c) => ({ ...c, [item.id]: !c[item.id] }))}
          >
            <View style={[styles.checkbox, checked[item.id] && styles.checkboxOn]}>
              {checked[item.id] ? <Text style={styles.checkmark}>✓</Text> : null}
            </View>
            <View style={styles.itemText}>
              <Text style={styles.itemName}>{item.name}</Text>
              <Text style={styles.itemDesc}>{item.description}</Text>
            </View>
            <Text style={styles.itemSize}>{formatBytes(sizes[item.id] ?? 0)}</Text>
          </PressableScale>
        ))}

        <Text style={styles.footnote}>
          Apple no deja que ninguna app limpie el sistema ni otras apps en iPhone — solo lo que
          guarda esta app. En Mac y Windows, VELY157 Cleaner sí limpia a fondo todo el equipo.
        </Text>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  scroll: { padding: 22, paddingBottom: 40 },
  header: { alignItems: 'center', marginBottom: 20 },
  logo: { color: colors.text, fontSize: 30, fontWeight: '700' },
  logoSub: { color: '#38BDF8', fontSize: 13, fontWeight: '700', letterSpacing: 2, marginTop: -2 },
  freeBadge: {
    marginTop: 10,
    paddingHorizontal: 12,
    paddingVertical: 5,
    borderRadius: 999,
    backgroundColor: 'rgba(34,197,94,0.15)',
    borderWidth: 1,
    borderColor: colors.green,
  },
  freeBadgeText: { color: colors.green, fontSize: 12, fontWeight: '700' },
  ring: {
    alignSelf: 'center',
    width: 220,
    height: 220,
    borderRadius: 110,
    borderWidth: 10,
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 16,
  },
  ringBig: { color: colors.text, fontSize: 32, fontWeight: '700' },
  ringSmall: { color: colors.muted, fontSize: 14, marginTop: 2 },
  progressTrack: { height: 8, borderRadius: 4, backgroundColor: colors.card, overflow: 'hidden' },
  progressFill: { height: '100%', backgroundColor: colors.blue },
  status: { color: colors.muted, textAlign: 'center', marginTop: 10, marginBottom: 18 },
  buttonsRow: { flexDirection: 'row', gap: 12, marginBottom: 24 },
  disabled: { opacity: 0.5 },
  secondaryButton: {
    flex: 1,
    height: 48,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: '#18264A',
    alignItems: 'center',
    justifyContent: 'center',
  },
  secondaryButtonText: { color: colors.text, fontWeight: '600', fontSize: 15 },
  primaryButton: {
    flex: 1,
    height: 48,
    borderRadius: 10,
    backgroundColor: colors.blue,
    alignItems: 'center',
    justifyContent: 'center',
  },
  primaryButtonText: { color: '#fff', fontWeight: '700', fontSize: 15 },
  sectionTitle: { color: colors.text, fontWeight: '700', fontSize: 16, marginBottom: 10 },
  itemRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: colors.line,
    padding: 12,
    marginBottom: 10,
  },
  checkbox: {
    width: 22,
    height: 22,
    borderRadius: 6,
    borderWidth: 1.5,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
  },
  checkboxOn: { backgroundColor: colors.blue, borderColor: colors.blue },
  checkmark: { color: '#fff', fontSize: 14, fontWeight: '700' },
  itemText: { flex: 1 },
  itemName: { color: colors.text, fontWeight: '600', fontSize: 14 },
  itemDesc: { color: colors.muted, fontSize: 11, marginTop: 2 },
  itemSize: { color: colors.muted, fontWeight: '600', marginLeft: 8 },
  footnote: { color: colors.muted, fontSize: 11, textAlign: 'center', marginTop: 10, lineHeight: 16 },
});
