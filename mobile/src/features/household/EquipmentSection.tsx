import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { Household, KitchenEquipment } from '@/api/types';
import { ErrorBanner } from '@/components/ErrorBanner';
import { MultiChoiceChips } from '@/components/MultiChoiceChips';
import { EQUIPMENT_OPTIONS } from '@/features/profile/labels';
import { toggle } from '@/features/profile/onboarding';
import { colors, spacing } from '@/theme';

/**
 * Équipement de la cuisine commune : tout membre peut le modifier, et chaque
 * changement est enregistré immédiatement.
 */
export function EquipmentSection({ household }: { household: Household }) {
  const [equipment, setEquipment] = useState<KitchenEquipment[]>(household.equipment);
  const [error, setError] = useState<string | null>(null);

  async function handleToggle(value: KitchenEquipment) {
    const previous = equipment;
    const next = toggle(equipment, value);
    // Mise à jour « optimiste » : l'écran réagit tout de suite, et revient en arrière si l'API refuse.
    setEquipment(next);
    setError(null);
    try {
      const saved = await api.households.updateEquipment(household.id, next);
      setEquipment(saved.equipment);
    } catch (e) {
      setEquipment(previous);
      setError(asApiError(e).message);
    }
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Équipement de la cuisine</Text>
      <Text style={styles.help}>Les recettes n'utiliseront que ce que vous avez. Commun à tout le foyer.</Text>
      <MultiChoiceChips options={EQUIPMENT_OPTIONS} values={equipment} onToggle={(v) => void handleToggle(v)} />
      <ErrorBanner message={error ?? undefined} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.sm },
  title: { fontSize: 18, fontWeight: '700', color: colors.text },
  help: { fontSize: 13, color: colors.mutedText },
});
