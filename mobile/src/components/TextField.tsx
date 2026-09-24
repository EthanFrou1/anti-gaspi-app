import { useState } from 'react';
import { Text, TextInput, View, type TextInputProps } from 'react-native';
import { makeStyles, useTheme } from '@/theme';

type TextFieldProps = TextInputProps & {
  label: string;
  error?: string;
};

export function TextField({ label, error, style, onFocus, onBlur, ...inputProps }: TextFieldProps) {
  const theme = useTheme();
  const styles = useStyles();
  // Champ actif : bord plus épais et plus foncé (repère visible sans dépendre de la couleur).
  const [focused, setFocused] = useState(false);

  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        style={[styles.input, focused && styles.inputFocused, error ? styles.inputError : null, style]}
        placeholderTextColor={theme.colors.ink3}
        selectionColor={theme.colors.primary}
        accessibilityLabel={label}
        onFocus={(event) => {
          setFocused(true);
          onFocus?.(event);
        }}
        onBlur={(event) => {
          setFocused(false);
          onBlur?.(event);
        }}
        {...inputProps}
      />
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

export const useFieldStyles = makeStyles((t) => ({
  container: { gap: t.space.xxs + 2 },
  label: { ...t.type.callout, color: t.colors.ink2 },
  input: {
    minHeight: 52,
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.md,
    paddingHorizontal: t.space.md,
    paddingVertical: t.space.sm,
    ...t.type.body,
    color: t.colors.ink,
  },
  inputFocused: { borderWidth: t.borderWidth.selected, borderColor: t.colors.border, paddingHorizontal: t.space.md - 0.5 },
  inputError: { borderWidth: t.borderWidth.selected, borderColor: t.colors.danger },
  hint: { ...t.type.caption, color: t.colors.ink3 },
  error: { ...t.type.caption, color: t.colors.danger },
}));

const useStyles = useFieldStyles;
