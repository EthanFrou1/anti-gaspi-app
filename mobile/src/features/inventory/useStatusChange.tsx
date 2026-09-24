import { useRef, useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { InventoryItem } from '@/api/types';
import { BottomSheet } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { Chip } from '@/components/Chip';
import { TextField } from '@/components/TextField';
import { makeStyles } from '@/theme';
import { formatQuantity, hasSeveralPortions, parseQuantity, portionOptions, remainingQuantity, unitLabel } from './rules';

export type StatusAction = 'consume' | 'discard';

type Pending = { item: InventoryItem; action: StatusAction };

/**
 * « Mangé » / « Jeté » d'un produit, partagé par le frigo et la fiche produit :
 * - une seule portion (1 pièce) : simple confirmation ;
 * - plusieurs portions : panneau « Combien ? » (tout, ou seulement une partie).
 *
 * L'appel à l'API part une fois le panneau refermé : sur iOS, ouvrir une autre Modal
 * (la célébration « Produit sauvé ») pendant qu'une Modal se ferme pose problème.
 * Renvoie ask (à appeler sur l'appui) et sheet (le panneau, à placer dans l'écran).
 */
export function useStatusChange(
  householdId: string | null,
  onDone: (item: InventoryItem, action: StatusAction) => void,
) {
  const [pending, setPending] = useState<Pending | null>(null);
  const [visible, setVisible] = useState(false);
  // Choix fait dans le panneau : null = tout, un nombre = une partie, undefined = fermé sans valider.
  const chosen = useRef<number | null | undefined>(undefined);

  async function run(item: InventoryItem, action: StatusAction, quantity: number | null) {
    try {
      await api.inventory[action](householdId!, item.id, quantity);
      onDone(item, action);
    } catch (e) {
      Alert.alert('Impossible de modifier ce produit', asApiError(e).message);
    }
  }

  function ask(item: InventoryItem, action: StatusAction) {
    if (hasSeveralPortions(item)) {
      chosen.current = undefined;
      setPending({ item, action });
      setVisible(true);
      return;
    }

    const consumed = action === 'consume';
    Alert.alert(consumed ? `${item.name} : consommé ?` : `${item.name} : jeté ?`, 'Il sera retiré du frigo.', [
      { text: 'Annuler', style: 'cancel' },
      {
        text: consumed ? 'Consommé' : 'Jeté',
        style: consumed ? 'default' : 'destructive',
        onPress: () => void run(item, action, null),
      },
    ]);
  }

  const sheet = pending ? (
    <PortionSheet
      // Nouveau produit : le choix repart de « Tout ».
      key={`${pending.item.id}-${pending.action}`}
      visible={visible}
      item={pending.item}
      action={pending.action}
      onClose={() => setVisible(false)}
      onConfirm={(quantity) => {
        chosen.current = quantity;
        setVisible(false);
      }}
      onHidden={() => {
        const quantity = chosen.current;
        setPending(null);
        if (quantity !== undefined) void run(pending.item, pending.action, quantity);
      }}
    />
  ) : null;

  return { ask, sheet };
}

type SheetProps = {
  visible: boolean;
  item: InventoryItem;
  action: StatusAction;
  onClose: () => void;
  // null = le produit entier.
  onConfirm: (quantity: number | null) => void;
  onHidden: () => void;
};

/** Panneau « Combien en as-tu mangé / jeté ? », dans l'unité du produit. « Tout » est choisi d'office. */
function PortionSheet({ visible, item, action, onClose, onConfirm, onHidden }: SheetProps) {
  const styles = useStyles();
  const consumed = action === 'consume';
  const options = portionOptions(item.quantity, item.unit);
  const [selected, setSelected] = useState<number | 'other'>(item.quantity);
  const [otherText, setOtherText] = useState('');
  const [error, setError] = useState<string | undefined>();

  const taken = selected === 'other' ? parseQuantity(otherText) : selected;
  const remaining = taken !== null && taken <= item.quantity ? remainingQuantity(item.quantity, taken) : null;

  function confirm() {
    if (taken === null) {
      setError('Quantité invalide (ex. 1, 0,5 ou 250).');
      return;
    }
    if (taken > item.quantity) {
      setError(`Il n'en reste que ${formatQuantity(item.quantity, item.unit)}.`);
      return;
    }
    onConfirm(taken >= item.quantity ? null : taken);
  }

  return (
    <BottomSheet
      visible={visible}
      title={consumed ? 'Combien en as-tu mangé ?' : 'Combien en as-tu jeté ?'}
      onClose={onClose}
      onHidden={onHidden}
    >
      <Text style={styles.product}>
        {item.name} · il en reste {formatQuantity(item.quantity, item.unit)}
      </Text>

      <View style={styles.row} accessibilityRole="radiogroup" accessibilityLabel="Quantité">
        {options.map((option) => (
          <Chip
            key={option.quantity}
            label={option.label}
            selected={selected === option.quantity}
            onPress={() => {
              setSelected(option.quantity);
              setError(undefined);
            }}
            accessibilityRole="radio"
          />
        ))}
        <Chip
          label="Autre…"
          selected={selected === 'other'}
          onPress={() => setSelected('other')}
          accessibilityRole="radio"
        />
      </View>

      {selected === 'other' ? (
        <TextField
          label={`Quantité (${unitLabel(item.unit)})`}
          placeholder="Ex. 0,5 ou 150"
          value={otherText}
          onChangeText={(text) => {
            setOtherText(text);
            setError(undefined);
          }}
          keyboardType="decimal-pad"
          autoFocus
          error={error}
        />
      ) : null}

      {remaining !== null ? (
        <Text style={styles.remaining} accessibilityLiveRegion="polite">
          {remaining === 0 ? 'Le produit sera retiré du frigo.' : `Il en restera ${formatQuantity(remaining, item.unit)}.`}
        </Text>
      ) : null}

      <Button title={consumed ? 'Valider' : 'Jeter'} variant={consumed ? 'primary' : 'danger'} onPress={confirm} />
    </BottomSheet>
  );
}

const useStyles = makeStyles((t) => ({
  product: { ...t.type.body, color: t.colors.ink2 },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xs },
  remaining: { ...t.type.callout, color: t.colors.ink },
}));
