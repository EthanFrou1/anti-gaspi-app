import { View } from 'react-native';
import { makeStyles } from '@/theme';
import { Chip } from './Chip';

type Option<T extends string> = { value: T; label: string };

type Props<T extends string> = {
  options: Option<T>[];
  value: T | null;
  onChange: (value: T) => void;
  disabled?: boolean;
};

/**
 * Choix unique parmi quelques options, sous forme de pastilles (unités, filtres…).
 * Générique : T est le type des valeurs possibles (ex. QuantityUnit).
 */
export function ChoiceChips<T extends string>({ options, value, onChange, disabled = false }: Props<T>) {
  const styles = useStyles();
  return (
    <View style={styles.row} accessibilityRole="radiogroup">
      {options.map((option) => (
        <Chip
          key={option.value}
          label={option.label}
          selected={option.value === value}
          onPress={() => onChange(option.value)}
          disabled={disabled}
          accessibilityRole="radio"
        />
      ))}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xs },
}));
