import { Text, View } from 'react-native';
import type { Urgency } from '@/features/inventory/rules';
import { brandUrgency, makeStyles, useTheme } from '@/theme';
import { stateIcons } from './icons/stateIcons';

type Props = {
  urgency: Urgency;
  // Libellé affiché ; par défaut, le libellé court de la charte (« Urgent », « À vérifier »…).
  label?: string;
};

/**
 * Badge d'urgence de la charte : TOUJOURS fond + icône + libellé, jamais la couleur seule.
 * Une DDM dépassée est « À vérifier » (œil, violet), jamais « Périmé » (règle anti-gaspi).
 */
export function UrgencyBadge({ urgency, label }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const key = brandUrgency(urgency);
  const { bg, fg, label: defaultLabel } = theme.urgency[key];
  const Icon = stateIcons[key];

  return (
    <View style={[styles.badge, { backgroundColor: bg }]}>
      <Icon width={16} height={16} color={fg} />
      <Text style={[styles.label, { color: fg }]}>{label ?? defaultLabel}</Text>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  badge: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    gap: t.space.xxs,
    borderRadius: t.radius.pill,
    paddingHorizontal: t.space.xs,
    paddingVertical: 3,
  },
  label: { ...t.type.caption },
}));
