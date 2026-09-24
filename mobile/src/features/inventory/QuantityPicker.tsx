import { useState } from 'react';
import { Text, View } from 'react-native';
import { Chip } from '@/components/Chip';
import { TextField, useFieldStyles } from '@/components/TextField';
import { makeStyles } from '@/theme';
import { presetQuantity, QUANTITY_PRESETS } from './rules';

type Props = {
  // Quantité en texte, comme avant (« 3 », « 0,5 ») : le formulaire la vérifie avec parseQuantity.
  value: string;
  onChange: (value: string) => void;
  error?: string;
  disabled?: boolean;
};

/**
 * Quantité en un appui : boutons 1 à 5 (« 3 pains », « 5 carottes »), et « Autre… » qui
 * affiche un champ classique pour toute autre valeur (0,5 kg, 250 g, 12…).
 */
export function QuantityPicker({ value, onChange, error, disabled = false }: Props) {
  const field = useFieldStyles();
  const styles = useStyles();
  // « Autre » est ouvert d'office si la quantité n'est pas un bouton (ex. 0,612 kg lu sur un ticket).
  const [other, setOther] = useState(() => presetQuantity(value) === null);
  const selected = other ? null : presetQuantity(value);

  return (
    <View style={field.container}>
      <Text style={field.label}>Quantité</Text>
      <View style={styles.row} accessibilityRole="radiogroup" accessibilityLabel="Quantité">
        {QUANTITY_PRESETS.map((preset) => (
          <Chip
            key={preset}
            label={String(preset)}
            selected={selected === preset}
            onPress={() => {
              setOther(false);
              onChange(String(preset));
            }}
            disabled={disabled}
            accessibilityRole="radio"
          />
        ))}
        <Chip
          label="Autre…"
          selected={other}
          onPress={() => {
            if (other) return;
            setOther(true);
            // Champ vide : l'utilisateur tape directement sa quantité.
            onChange('');
          }}
          disabled={disabled}
          accessibilityRole="radio"
        />
      </View>
      {other ? (
        <TextField
          label="Autre quantité"
          placeholder="Ex. 0,5 ou 250"
          value={value}
          onChangeText={onChange}
          keyboardType="decimal-pad"
          // Ouvre le clavier seulement quand l'utilisateur vient de choisir « Autre ».
          autoFocus={value === ''}
          editable={!disabled}
        />
      ) : null}
      {error ? <Text style={field.error}>{error}</Text> : null}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xs },
}));
