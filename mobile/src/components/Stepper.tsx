import { Pressable, Text, View } from 'react-native';
import { Minus, Plus } from '@/components/icons/lucide';
import { makeStyles, useTheme } from '@/theme';

type Props = {
  value: number;
  min: number;
  max: number;
  onChange: (value: number) => void;
  // Lecteur d'écran : « 2 invités », « Un invité de plus », « Un invité de moins ».
  valueLabel: string;
  incrementLabel: string;
  decrementLabel: string;
};

/** Compteur « − N + » (portions, invités), borné entre min et max. */
export function Stepper({ value, min, max, onChange, valueLabel, incrementLabel, decrementLabel }: Props) {
  const styles = useStyles();
  const set = (next: number) => onChange(Math.min(max, Math.max(min, next)));

  return (
    <View style={styles.stepper}>
      <StepperButton icon="minus" label={decrementLabel} onPress={() => set(value - 1)} disabled={value <= min} />
      <Text style={styles.value} accessibilityLabel={valueLabel}>
        {value}
      </Text>
      <StepperButton icon="plus" label={incrementLabel} onPress={() => set(value + 1)} disabled={value >= max} />
    </View>
  );
}

type ButtonProps = { icon: 'plus' | 'minus'; label: string; onPress: () => void; disabled: boolean };

function StepperButton({ icon, label, onPress, disabled }: ButtonProps) {
  const theme = useTheme();
  const styles = useStyles();
  const Icon = icon === 'plus' ? Plus : Minus;
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled }}
      style={[styles.button, disabled && styles.disabled]}
    >
      <Icon size={22} strokeWidth={2.5} color={theme.colors.ink} />
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  stepper: { flexDirection: 'row', alignItems: 'center', gap: t.space.xl },
  button: {
    width: t.layout.minTouch,
    height: t.layout.minTouch,
    borderRadius: t.layout.minTouch / 2,
    borderWidth: t.borderWidth.selected,
    borderColor: t.colors.border,
    backgroundColor: t.colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  value: { ...t.type.title1, color: t.colors.ink, minWidth: 40, textAlign: 'center' },
  disabled: { opacity: 0.4 },
}));
