// AsyncStorage est un module natif, absent sous Jest : on utilise le simulateur fourni
// par la bibliothèque (stockage en mémoire, remis à zéro par AsyncStorage.clear()).
jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock'),
);
