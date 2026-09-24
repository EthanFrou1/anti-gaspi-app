import { View } from 'react-native';
import { makeStyles } from '@/theme';
import { Chip } from './Chip';

type Option<T extends string> = { value: T; label: string };

type Props<T extends string> = {
  options: Option<T>[];
  values: readonly T[];
  onToggle: (value: T) => void;
  disabled?: boolean;
};

/**
 * Choix multiple sous forme de pastilles (allergies, exclusions, équipement…).
 */
export function MultiChoiceChips<T extends string>({ options, values, onToggle, disabled = false }: Props<T>) {
  const styles = useStyles();
  return (
    <View style={styles.row}>
      {options.map((option) => (
        <Chip
          key={option.value}
          label={option.label}
          selected={values.includes(option.value)}
          onPress={() => onToggle(option.value)}
          disabled={disabled}
          accessibilityRole="checkbox"
        />
      ))}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xs },
}));
