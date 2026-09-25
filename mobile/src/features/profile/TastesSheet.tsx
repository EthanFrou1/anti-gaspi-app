import { ScrollView, Text, useWindowDimensions, View } from 'react-native';
import type { Profile } from '@/api/types';
import { BottomSheet } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { makeStyles } from '@/theme';
import { DislikesGrid } from './DislikesGrid';
import { toggle } from './onboarding';

type Props = {
  visible: boolean;
  profile: Profile | null;
  onChange: (profile: Profile) => void;
  // « Enregistrer et proposer » : enregistre les goûts, puis lance la recette.
  onSave: () => void;
  // « Plus tard » : ne plus poser la question, et lancer la recette.
  onLater: () => void;
  // Fond assombri ou bouton retour : ni réponse, ni recette (la question reviendra).
  onClose: () => void;
  onHidden: () => void;
  saving: boolean;
  error: string | null;
};

/** Question posée une fois, juste avant la première recette. */
export function TastesSheet({ visible, profile, onChange, onSave, onLater, onClose, onHidden, saving, error }: Props) {
  const styles = useStyles();
  const { height } = useWindowDimensions();

  return (
    <BottomSheet
      visible={visible}
      title="Avant ta première recette : il y a des aliments que tu n'aimes pas ?"
      onClose={onClose}
      onHidden={onHidden}
    >
      {/* La grille dépasse la hauteur de l'écran : elle défile, les boutons restent visibles. */}
      <ScrollView style={{ maxHeight: height * 0.55 }} contentContainerStyle={styles.scroll}>
        {profile ? (
          <DislikesGrid
            dislikes={profile.dislikes}
            avoidSpicy={profile.avoidSpicy}
            onToggleFood={(food) => onChange({ ...profile, dislikes: toggle(profile.dislikes, food) })}
            onAvoidSpicyChange={(avoidSpicy) => onChange({ ...profile, avoidSpicy })}
          />
        ) : null}
      </ScrollView>
      <ErrorBanner message={error ?? undefined} />
      <View style={styles.actions}>
        <Button title="Enregistrer et proposer" onPress={onSave} loading={saving} />
        <Button title="Plus tard" variant="secondary" onPress={onLater} disabled={saving} />
        <Text style={styles.hint}>Tu pourras changer d'avis dans Profil → « Ce que je n'aime pas ».</Text>
      </View>
    </BottomSheet>
  );
}

const useStyles = makeStyles((t) => ({
  scroll: { paddingBottom: t.space.sm },
  actions: { gap: t.space.sm },
  hint: { ...t.type.caption, color: t.colors.ink3, textAlign: 'center' },
}));
