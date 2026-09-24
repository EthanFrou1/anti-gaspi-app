import type { LucideIcon } from 'lucide-react-native';
import type { ReactNode } from 'react';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import { makeStyles, useTheme } from '@/theme';
import { Check, ChevronRight } from './icons/lucide';

/**
 * Liste de réglages par sections, comme les Réglages d'iOS : titre de section en petites
 * capitales, puis une carte qui regroupe les lignes, séparées par un trait fin.
 */
export function SettingsSection({ title, footer, children }: { title: string; footer?: string; children: ReactNode }) {
  const styles = useStyles();
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle} accessibilityRole="header">
        {title}
      </Text>
      <View style={styles.card}>{children}</View>
      {footer ? <Text style={styles.footer}>{footer}</Text> : null}
    </View>
  );
}

type RowProps = {
  icon: LucideIcon;
  label: string;
  // Valeur actuelle affichée à droite (ex. « Automatique », « 1.0.0 »).
  value?: string;
  // Sans onPress : ligne d'information (pas de chevron, pas cliquable).
  onPress?: () => void;
  accessibilityHint?: string;
  // Action irréversible (suppression du compte) : icône et libellé en rouge.
  destructive?: boolean;
  loading?: boolean;
  // Dernière ligne de la section : pas de trait dessous.
  last?: boolean;
  // Ligne d'un choix unique (ex. le thème) : coche si choisie, pas de chevron, rôle « radio ».
  selected?: boolean;
};

export function SettingsRow({ icon: Icon, label, value, onPress, accessibilityHint, destructive = false, loading = false, last = false, selected }: RowProps) {
  const theme = useTheme();
  const styles = useStyles();
  const color = destructive ? theme.colors.danger : theme.colors.ink;
  const isChoice = selected !== undefined;

  const content = (
    <>
      <View style={[styles.iconTile, destructive && styles.iconTileDanger]}>
        <Icon size={20} strokeWidth={2} color={destructive ? theme.colors.danger : theme.colors.primaryText} />
      </View>
      <Text style={[styles.label, { color }]}>{label}</Text>
      {value ? (
        <Text style={styles.value} numberOfLines={1}>
          {value}
        </Text>
      ) : null}
      {loading ? <ActivityIndicator color={theme.colors.primary} /> : null}
      {/* Choix actuel : une coche (jamais la couleur seule). */}
      {selected ? <Check size={22} strokeWidth={2.5} color={theme.colors.ink} /> : null}
      {!loading && !isChoice && onPress ? <ChevronRight size={20} strokeWidth={2} color={theme.colors.ink3} /> : null}
    </>
  );

  if (!onPress) {
    return (
      <View style={[styles.row, !last && styles.separator]} accessible accessibilityLabel={value ? `${label} : ${value}` : label}>
        {content}
      </View>
    );
  }
  return (
    <Pressable
      onPress={onPress}
      disabled={loading}
      accessibilityRole={isChoice ? 'radio' : 'button'}
      accessibilityLabel={value ? `${label} : ${value}` : label}
      accessibilityHint={accessibilityHint}
      accessibilityState={isChoice ? { selected } : { busy: loading }}
      style={({ pressed }) => [styles.row, !last && styles.separator, pressed && styles.pressed]}
    >
      {content}
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  section: { gap: t.space.xs },
  sectionTitle: { ...t.type.overline, color: t.colors.ink3, marginLeft: t.space.xs },
  card: {
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.lg,
    overflow: 'hidden',
  },
  footer: { ...t.type.caption, color: t.colors.ink3, marginHorizontal: t.space.xs },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: t.space.sm,
    minHeight: 56,
    paddingHorizontal: t.space.md,
    paddingVertical: t.space.xs,
  },
  separator: { borderBottomWidth: t.borderWidth.hairline, borderBottomColor: t.colors.line },
  pressed: { backgroundColor: t.colors.surface2 },
  iconTile: {
    width: 34,
    height: 34,
    borderRadius: 10,
    backgroundColor: t.colors.primarySoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  iconTileDanger: { backgroundColor: t.scheme === 'dark' ? t.palette.framboise.darkSoft : t.palette.framboise.soft },
  label: { ...t.type.body, flex: 1 },
  // La valeur ne prend jamais la place du libellé : une ligne, 40 % de la largeur au plus.
  value: { ...t.type.callout, color: t.colors.ink3, flexShrink: 1, maxWidth: '40%', textAlign: 'right' },
}));
