import DateTimePicker, { DateTimePickerAndroid } from '@react-native-community/datetimepicker';
import { useState } from 'react';
import { Platform, Pressable, Text, View } from 'react-native';
import { formatShortDate, fromDateString, toLocalDateString } from '@/utils/dates';
import { makeStyles, useTheme } from '@/theme';
import { Chip } from './Chip';
import { useFieldStyles } from './TextField';

export type DateShortcut = { label: string; value: string };

type Props = {
  label: string;
  // « AAAA-MM-JJ »
  value: string;
  onChange: (value: string) => void;
  // Raccourcis rapides (ex. « Aujourd'hui », « +3 j », « +1 sem. »).
  shortcuts?: DateShortcut[];
  minimumDate?: string;
  maximumDate?: string;
  hint?: string;
  error?: string;
  disabled?: boolean;
};

/**
 * Champ de date : raccourcis en un appui, plus le sélecteur natif pour une date précise.
 * - Android : boîte de dialogue système (API impérative recommandée par la bibliothèque).
 * - iOS : calendrier affiché sous le champ.
 */
export function DateField({ label, value, onChange, shortcuts = [], minimumDate, maximumDate, hint, error, disabled }: Props) {
  const theme = useTheme();
  const field = useFieldStyles();
  const styles = useStyles();
  const [iosPickerOpen, setIosPickerOpen] = useState(false);

  const min = minimumDate ? fromDateString(minimumDate) : undefined;
  const max = maximumDate ? fromDateString(maximumDate) : undefined;

  function openPicker() {
    if (Platform.OS === 'android') {
      DateTimePickerAndroid.open({
        value: fromDateString(value),
        mode: 'date',
        minimumDate: min,
        maximumDate: max,
        onValueChange: (_event, date) => onChange(toLocalDateString(date)),
      });
    } else {
      setIosPickerOpen((open) => !open);
    }
  }

  return (
    <View style={field.container}>
      <Text style={field.label}>{label}</Text>

      <Pressable
        onPress={openPicker}
        disabled={disabled}
        accessibilityRole="button"
        accessibilityLabel={`${label} : ${formatShortDate(value)}. Modifier`}
        style={[field.input, styles.value, error ? field.inputError : null, disabled && styles.disabled]}
      >
        <Text style={styles.valueText}>{formatShortDate(value)}</Text>
        <Text style={styles.change}>Choisir…</Text>
      </Pressable>

      {shortcuts.length > 0 && !disabled ? (
        <View style={styles.shortcuts}>
          {shortcuts.map((shortcut) => (
            <Chip
              key={shortcut.label}
              label={shortcut.label}
              selected={shortcut.value === value}
              onPress={() => {
                onChange(shortcut.value);
                setIosPickerOpen(false);
              }}
              accessibilityRole="button"
            />
          ))}
        </View>
      ) : null}

      {Platform.OS === 'ios' && iosPickerOpen && !disabled ? (
        <DateTimePicker
          value={fromDateString(value)}
          mode="date"
          display="inline"
          accentColor={theme.colors.primary}
          themeVariant={theme.scheme}
          minimumDate={min}
          maximumDate={max}
          onValueChange={(_event, date) => {
            onChange(toLocalDateString(date));
            // Un jour choisi : le calendrier se referme (changer de mois ne déclenche pas cet appel).
            setIosPickerOpen(false);
          }}
        />
      ) : null}

      {hint && !error ? <Text style={field.hint}>{hint}</Text> : null}
      {error ? <Text style={field.error}>{error}</Text> : null}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  value: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  disabled: { opacity: 0.6 },
  valueText: { ...t.type.body, color: t.colors.ink },
  change: { ...t.type.callout, color: t.colors.primaryText },
  shortcuts: { flexDirection: 'row', gap: t.space.xs, flexWrap: 'wrap' },
}));
