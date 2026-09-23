import DateTimePicker, { DateTimePickerAndroid } from '@react-native-community/datetimepicker';
import { useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { formatShortDate, fromDateString, toLocalDateString } from '@/utils/dates';
import { colors, spacing } from '@/theme';

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
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>

      <Pressable
        onPress={openPicker}
        disabled={disabled}
        accessibilityRole="button"
        accessibilityLabel={`${label} : ${formatShortDate(value)}. Modifier`}
        style={[styles.value, error ? styles.valueError : null, disabled && styles.disabled]}
      >
        <Text style={styles.valueText}>{formatShortDate(value)}</Text>
        <Text style={styles.change}>Choisir…</Text>
      </Pressable>

      {shortcuts.length > 0 && !disabled ? (
        <View style={styles.shortcuts}>
          {shortcuts.map((shortcut) => (
            <Pressable
              key={shortcut.label}
              onPress={() => {
                onChange(shortcut.value);
                setIosPickerOpen(false);
              }}
              accessibilityRole="button"
              style={[styles.shortcut, shortcut.value === value && styles.shortcutSelected]}
            >
              <Text style={[styles.shortcutText, shortcut.value === value && styles.shortcutTextSelected]}>
                {shortcut.label}
              </Text>
            </Pressable>
          ))}
        </View>
      ) : null}

      {Platform.OS === 'ios' && iosPickerOpen && !disabled ? (
        <DateTimePicker
          value={fromDateString(value)}
          mode="date"
          display="inline"
          minimumDate={min}
          maximumDate={max}
          onValueChange={(_event, date) => onChange(toLocalDateString(date))}
        />
      ) : null}

      {hint && !error ? <Text style={styles.hint}>{hint}</Text> : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.xs },
  label: { fontSize: 14, fontWeight: '600', color: colors.text },
  value: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm + 4,
  },
  valueError: { borderColor: colors.error },
  disabled: { opacity: 0.6 },
  valueText: { fontSize: 16, color: colors.text },
  change: { fontSize: 14, color: colors.primary, fontWeight: '600' },
  shortcuts: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  shortcut: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 16,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs + 2,
  },
  shortcutSelected: { backgroundColor: colors.primary, borderColor: colors.primary },
  shortcutText: { fontSize: 14, color: colors.text },
  shortcutTextSelected: { color: colors.primaryText, fontWeight: '600' },
  hint: { fontSize: 13, color: colors.mutedText },
  error: { fontSize: 13, color: colors.error },
});
