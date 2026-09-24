import { useState } from 'react';
import { Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { Household, KitchenEquipment } from '@/api/types';
import { ErrorBanner } from '@/components/ErrorBanner';
import { MultiChoiceChips } from '@/components/MultiChoiceChips';
import { EQUIPMENT_OPTIONS } from '@/features/profile/labels';
import { toggle } from '@/features/profile/onboarding';
import { makeStyles } from '@/theme';

/**
 * Équipement de la cuisine commune : tout membre peut le modifier, et chaque
 * changement est enregistré immédiatement.
 */
export function EquipmentSection({ household }: { household: Household }) {
  const styles = useStyles();
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

const useStyles = makeStyles((t) => ({
  container: { gap: t.space.xs },
  title: { ...t.type.title3, color: t.colors.ink },
  help: { ...t.type.caption, color: t.colors.ink3 },
}));
