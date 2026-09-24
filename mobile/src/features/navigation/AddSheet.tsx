import type { LucideIcon } from 'lucide-react-native';
import { Pressable, Text, View } from 'react-native';
import { BottomSheet } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { ChefHat, ChevronRight, PencilLine, ReceiptText, ScanBarcode } from '@/components/icons/lucide';
import { makeStyles, useTheme } from '@/theme';

export type AddAction = 'scanProduct' | 'manualEntry' | 'suggestRecipe' | 'scanReceipt' | 'goToHousehold';

type Props = {
  visible: boolean;
  // Sans foyer, il n'y a pas encore de frigo : le panneau renvoie vers l'onglet Foyer.
  hasHousehold: boolean;
  onClose: () => void;
  // Action choisie ; la navigation se fait une fois le panneau refermé (voir onHidden).
  onSelect: (action: AddAction) => void;
  onHidden: () => void;
};

const ACTIONS: { action: AddAction; title: string; description: string; Icon: LucideIcon }[] = [
  { action: 'scanProduct', title: 'Scanner un produit', description: 'Lis le code-barres d\'un produit', Icon: ScanBarcode },
  { action: 'manualEntry', title: 'Saisir à la main', description: 'Nom, catégorie et date, sans scanner', Icon: PencilLine },
  { action: 'suggestRecipe', title: 'Proposer une recette', description: 'Avec ce qui périme en premier', Icon: ChefHat },
  { action: 'scanReceipt', title: 'Scanner un ticket', description: 'Toutes tes courses en une photo', Icon: ReceiptText },
];

/** Panneau du bouton « + » : les façons d'ajouter au frigo, et la suggestion de recette. */
export function AddSheet({ visible, hasHousehold, onClose, onSelect, onHidden }: Props) {
  const theme = useTheme();
  const styles = useStyles();

  return (
    <BottomSheet visible={visible} title={hasHousehold ? 'Ajouter' : 'Pas encore de frigo'} onClose={onClose} onHidden={onHidden}>
      {hasHousehold ? (
        <View style={styles.list}>
          {ACTIONS.map(({ action, title, description, Icon }) => (
            <Pressable
              key={action}
              onPress={() => onSelect(action)}
              accessibilityRole="button"
              accessibilityLabel={title}
              accessibilityHint={description}
              style={({ pressed }) => [styles.row, pressed && styles.rowPressed]}
            >
              <View style={styles.iconTile}>
                <Icon size={24} strokeWidth={2} color={theme.colors.primaryText} />
              </View>
              <View style={styles.text}>
                <Text style={styles.title}>{title}</Text>
                <Text style={styles.description}>{description}</Text>
              </View>
              <ChevronRight size={20} strokeWidth={2} color={theme.colors.ink3} />
            </Pressable>
          ))}
        </View>
      ) : (
        <View style={styles.list}>
          <Text style={styles.description}>Crée ton foyer ou rejoins celui de ta coloc pour commencer à remplir le frigo.</Text>
          <Button title="Aller à l'onglet Foyer" onPress={() => onSelect('goToHousehold')} />
        </View>
      )}
    </BottomSheet>
  );
}

const useStyles = makeStyles((t) => ({
  list: { gap: t.space.xs },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: t.space.sm,
    minHeight: 64,
    paddingVertical: t.space.xs,
    paddingHorizontal: t.space.xs,
    borderRadius: t.radius.md,
  },
  rowPressed: { backgroundColor: t.colors.surface2 },
  iconTile: {
    width: 44,
    height: 44,
    borderRadius: t.radius.sm,
    backgroundColor: t.colors.primarySoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  text: { flex: 1, gap: 2 },
  title: { ...t.type.card, color: t.colors.ink },
  description: { ...t.type.caption, color: t.colors.ink3 },
}));
