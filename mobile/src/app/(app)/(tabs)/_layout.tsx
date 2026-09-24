import { router, Tabs } from 'expo-router';
import { useRef, useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { addActionTarget } from '@/features/navigation/addActions';
import { AddSheet, type AddAction } from '@/features/navigation/AddSheet';
import { TabBar } from '@/features/navigation/TabBar';
import { useTheme } from '@/theme';

/**
 * Onglets Frigo, Recettes, Foyer et Profil, avec le bouton « + » central (barre de la charte).
 */
export default function TabsLayout() {
  const theme = useTheme();
  const { state } = useAuth();
  const hasHousehold = state.status === 'signedIn' && state.user.householdId !== null;
  const [addOpen, setAddOpen] = useState(false);
  // Action choisie dans le panneau, exécutée une fois le panneau refermé.
  const pendingAction = useRef<AddAction | null>(null);

  function openTarget() {
    const action = pendingAction.current;
    pendingAction.current = null;
    if (!action) return;
    const target = addActionTarget(action);
    if (target.mode === 'push') {
      router.push(target.href);
    } else {
      router.navigate(target.href);
    }
  }

  return (
    <>
      <Tabs
        tabBar={(props) => <TabBar {...props} onAddPress={() => setAddOpen(true)} />}
        screenOptions={{
          headerStyle: { backgroundColor: theme.colors.bg },
          headerShadowVisible: false,
          headerTintColor: theme.colors.ink,
          headerTitleStyle: { fontFamily: theme.fonts.heading, fontSize: theme.type.title3.fontSize, color: theme.colors.ink },
          sceneStyle: { backgroundColor: theme.colors.bg },
        }}
      >
        <Tabs.Screen name="index" options={{ title: 'Frigo' }} />
        <Tabs.Screen name="recipes" options={{ title: 'Recettes' }} />
        <Tabs.Screen name="household" options={{ title: 'Foyer' }} />
        <Tabs.Screen name="profile" options={{ title: 'Profil' }} />
      </Tabs>

      <AddSheet
        visible={addOpen}
        hasHousehold={hasHousehold}
        onClose={() => setAddOpen(false)}
        onSelect={(action) => {
          pendingAction.current = action;
          setAddOpen(false);
        }}
        onHidden={openTarget}
      />
    </>
  );
}
