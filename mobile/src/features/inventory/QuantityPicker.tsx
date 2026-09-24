import { useState } from 'react';
import { Text, View } from 'react-native';
import type { QuantityUnit } from '@/api/types';
import { ChoiceChips } from '@/components/ChoiceChips';
import { Chip } from '@/components/Chip';
import { TextField, useFieldStyles } from '@/components/TextField';
import { makeStyles } from '@/theme';
import { presetQuantity, QUANTITY_PRESETS, quantityAfterUnitChange, toQuantityText, UNITS, unitLabel } from './rules';

type Props = {
  // Quantité en texte, comme avant (« 3 », « 0,5 ») : le formulaire la vérifie avec parseQuantity.
  value: string;
  onChange: (value: string) => void;
  unit: QuantityUnit;
  onUnitChange: (unit: QuantityUnit) => void;
  error?: string;
  disabled?: boolean;
};

/**
 * Quantité en deux appuis : l'unité d'abord (pièce, g, kg, ml, l), puis un bouton adapté à
 * cette unité (1 à 5 pièces, 250 g, 1 l…). « Autre… » affiche un champ classique pour toute
 * autre valeur (0,612 kg, 12 pièces…).
 */
export function QuantityPicker({ value, onChange, unit, onUnitChange, error, disabled = false }: Props) {
  const field = useFieldStyles();
  const styles = useStyles();
  // « Autre » est ouvert d'office si la quantité n'est pas un bouton (ex. 0,612 kg lu sur un ticket).
  const [other, setOther] = useState(() => presetQuantity(value, unit) === null);
  const selected = other ? null : presetQuantity(value, unit);

  function changeUnit(next: QuantityUnit) {
    // Une valeur tapée dans « Autre » est gardée telle quelle : l'utilisateur corrige souvent l'unité après coup.
    if (!other) {
      onChange(quantityAfterUnitChange(value, unit, next));
    }
    onUnitChange(next);
  }

  return (
    <View style={field.container}>
      <Text style={field.label}>Quantité</Text>
      <ChoiceChips
        options={UNITS.map((u) => ({ value: u, label: unitLabel(u) }))}
        value={unit}
        onChange={changeUnit}
        disabled={disabled}
      />
      <View style={styles.row} accessibilityRole="radiogroup" accessibilityLabel="Quantité">
        {QUANTITY_PRESETS[unit].map((preset) => (
          <Chip
            key={preset}
            // Pièces : le nombre seul ; sinon avec l'unité (« 250 g », « 0,5 kg »).
            label={unit === 'Piece' ? String(preset) : `${preset.toLocaleString('fr-FR')} ${unitLabel(unit)}`}
            selected={selected === preset}
            onPress={() => {
              setOther(false);
              onChange(toQuantityText(preset));
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
          label={`Autre quantité (${unitLabel(unit)})`}
          placeholder="Ex. 0,5 ou 12"
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
