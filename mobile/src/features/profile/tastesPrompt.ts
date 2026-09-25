import AsyncStorage from '@react-native-async-storage/async-storage';
import type { Profile } from '@/api/types';

/**
 * Question « Avant ta première recette : il y a des aliments que tu n'aimes pas ? », posée une
 * seule fois. Mémoire locale au téléphone (par utilisateur) : sur un autre téléphone ou après
 * une réinstallation, elle revient une fois, ce qui est sans gravité.
 */

const storageKey = (userId: string) => `leftly.tastesPromptSeen.${userId}`;

/** À demander si la question n'a jamais eu de réponse et que le profil n'a encore aucun goût. */
export function shouldAskTastes(profile: Pick<Profile, 'dislikes' | 'avoidSpicy'>, alreadySeen: boolean): boolean {
  return !alreadySeen && profile.dislikes.length === 0 && !profile.avoidSpicy;
}

/** true si la question a déjà eu une réponse. En cas d'erreur de stockage : true (ne pas insister). */
export async function loadTastesPromptSeen(userId: string): Promise<boolean> {
  try {
    return (await AsyncStorage.getItem(storageKey(userId))) === '1';
  } catch {
    return true;
  }
}

export async function markTastesPromptSeen(userId: string): Promise<void> {
  try {
    await AsyncStorage.setItem(storageKey(userId), '1');
  } catch {
    // Sans enregistrement, la question pourra revenir une fois : rien de bloquant.
  }
}
