# CLAUDE.md — App anti-gaspi (nom provisoire)

Ce fichier donne le contexte du projet à Claude Code. Lis-le entièrement avant toute action.

## Le projet

Application mobile anti-gaspillage alimentaire. L'utilisateur tient à jour l'inventaire de son frigo (scan de code-barres, saisie manuelle, puis scan de ticket de caisse par IA). L'app le prévient avant la péremption des produits et lui propose des recettes qui utilisent **en priorité ce qui périme en premier**, adaptées à son profil (temps, budget, équipement, régime, objectif).

### Ce qui nous différencie

- **Zéro friction de saisie** : scan de ticket de caisse lu par une IA (V2), dates de péremption estimées automatiquement.
- **Priorité anti-gaspi** : les suggestions partent de ce qui va périmer, pas seulement de ce qui est disponible.
- **Multi-profils via un moteur générique** : étudiants, colocs, familles, sportifs… sont gérés par la même logique, pilotée par le foyer et le profil. Aucun "mode" codé en dur par type d'utilisateur.
- **Gaspillage visible** : compteur des produits sauvés et de l'argent économisé.

Cible de lancement : étudiants et alternants (petit budget, peu de temps, peu d'ustensiles). Le produit reste pensé pour tous les profils.

### Objectif et contexte

- **Objectif** : projet portfolio, avec un petit revenu possible (modèle freemium). La qualité du code, la sécurité et la documentation comptent autant que les fonctionnalités.
- **Coûts** : garder les coûts d'hébergement et d'IA au minimum. Toute fonctionnalité qui appelle l'IA doit être limitée par utilisateur.
- **Concurrence directe** : Friio (inventaire, tri par urgence, frigo partagé, recettes). Notre différence : scan de ticket par IA, recettes générées selon le profil, cible étudiante.
- **Nom** : provisoire, repo `anti-gaspi-app`. Le nom définitif et la DA viendront plus tard ; ne pas coder le nom de l'app en dur partout (le centraliser dans la config).

## Stack technique

- **Backend** : API REST en C# / .NET (dernière version LTS), ASP.NET Core Web API.
- **ORM** : Entity Framework Core.
- **Base de données** : PostgreSQL (à confirmer).
- **Authentification** : JWT (access token + refresh token), via ASP.NET Core Identity (à confirmer).
- **Mobile** : React Native avec Expo, en TypeScript.
- **Codes-barres** : API Open Food Facts (nom, catégorie, infos produit).
- **IA** : un modèle de langage avec vision, appelé **uniquement depuis le backend**, pour lire les tickets de caisse et générer les recettes.
- **Notifications** : Expo Notifications. Notifications **locales** au MVP (programmées par le téléphone), push en V2.

## Structure du repo

```
/api        → projet ASP.NET Core Web API
/mobile     → app Expo (React Native, TypeScript)
CLAUDE.md
```

## Modèle de domaine

```
Utilisateur ── appartient à ──> Foyer (1 ou plusieurs membres)
Utilisateur ── possède ──> Profil
Foyer ── contient ──> Produit
Recette générée ← produits du foyer + profil(s) des personnes qui mangent
```

- **Foyer** : l'inventaire appartient au foyer, pas à l'utilisateur. Un utilisateur peut inviter d'autres membres (code ou lien d'invitation). Au MVP, un utilisateur appartient à un seul foyer à la fois.
- **Invitations** : tout membre peut créer un code (valable 7 jours, 10 actifs maximum par foyer). Un membre peut révoquer ses propres invitations, le propriétaire peut toutes les révoquer. Seul le propriétaire peut exclure un membre ; une exclusion révoque toutes les invitations actives du foyer (sinon l'exclu pourrait revenir avec un code connu). Quand le propriétaire part, les invitations restent valables.
- **Propriété du foyer** : si le propriétaire quitte le foyer ou supprime son compte, la propriété passe au membre le plus ancien (date d'arrivée dans le foyer). S'il n'y a plus aucun membre, le foyer et tout son contenu sont supprimés. Ces cas sont couverts par des tests.
- **Produit** : nom, catégorie, quantité, unité, date d'achat, date de péremption (estimée ou saisie), code-barres optionnel, **propriétaire optionnel**. Propriétaire vide = produit commun au foyer ; renseigné = produit perso (cas des colocs).
- **Produits perso** : visibles par tout le foyer, mais seul leur propriétaire peut les modifier, les marquer consommés/jetés ou les supprimer. Quand il quitte le foyer ou supprime son compte, ses produits deviennent communs (la nourriture reste dans le frigo).
- **Statut d'un produit** : actif, consommé ou jeté. « Consommé » et « jeté » concernent le produit entier (la quantité se modifie par l'édition) et sont conservés en base pour le futur compteur de gaspillage ; la suppression sert seulement à corriger une erreur de saisie.
- **Profil** (privé, jamais montré aux autres membres) : temps de cuisine souhaité, budget par repas (tranches), régime, exclusions d'ingrédients, allergies, objectif (équilibré, prise de muscle, repas légers, anti-gaspi simple), nombre de portions par défaut. Uniquement des listes fermées, **aucun texte libre** (il finirait dans le prompt de l'IA : risque d'injection de prompt).
- **Équipement de cuisine** : porté par le **foyer** (plaques, four, micro-ondes, air fryer, blender), car les membres partagent la même cuisine ; modifiable par tout membre.
- **Données sensibles (RGPD, article 9)** : les allergies sont des données de santé, enregistrées seulement avec un consentement explicite (date conservée ; retirer le consentement efface les allergies). Pas de régime « religieux » (halal, casher) : des exclusions d'ingrédients neutres (porc, alcool…) couvrent le besoin sans étiqueter la personne.
- **Repas partagé** : quand plusieurs membres mangent ensemble, les contraintes de leurs profils se combinent (si l'un est végétarien, la recette l'est ; les allergies de tous sont exclues).

### Estimation des dates de péremption

Chaque catégorie de produit a une durée de conservation par défaut (ex. viande fraîche ≈ 3 jours, yaourt ≈ 3 semaines). La date est estimée à l'ajout ; l'utilisateur peut la corriger. Les durées sont stockées en base (table de référence), pas en dur dans le code.

**Open Food Facts** : appelé uniquement par l'API (cache, limite par utilisateur et limite globale de 15 requêtes/min, car tous les appels partent de l'IP du serveur). L'image du produit n'est affichée que sur l'écran de confirmation après un scan : jamais dans la liste du frigo, et son URL n'est pas stockée avec le produit.

Chaque catégorie indique aussi son type de date : **DLC** (« à consommer jusqu'au », à ne pas dépasser) ou **DDM** (« à consommer de préférence avant », encore consommable après). Une DDM dépassée ne doit jamais être présentée comme « périmée » : c'est un levier anti-gaspi. La correspondance entre catégories Open Food Facts et catégories de l'app est elle aussi en base.

## Périmètre du MVP (ordre de développement)

1. **Comptes et foyers** : inscription, connexion, création d'un foyer, invitation de membres.
2. **Inventaire** : ajout par code-barres (Open Food Facts) et par saisie manuelle, dates de péremption estimées, modification et suppression, marquer un produit comme consommé ou jeté.
3. **Onboarding et profil** : 4 à 5 questions à l'inscription, modifiables ensuite.
4. **Suggestions de recettes par IA** : priorité aux produits qui périment en premier, respect des contraintes du ou des profils.
5. **Notifications** : alerte avant péremption.

### V2 (après le MVP)

- **Première priorité : notifications push, avec le passage à EAS Build et la distribution sur TestFlight.** Le compte Apple Developer (payant) existe déjà. Le push lèvera la limite des notifications locales (un produit ajouté par un colocataire n'est pris en compte qu'à la prochaine ouverture de l'app). À prévoir : development build (le push ne marche plus dans Expo Go depuis le SDK 53), identifiants APNs (iOS) et FCM (Android, projet Firebase), projectId EAS, table des jetons d'appareils côté API (supprimés à la déconnexion et à la suppression du compte), tâche quotidienne d'envoi, reçus de livraison. Déclarer alors le plugin `expo-notifications` dans `app.config.ts` (il ajoute l'autorisation push iOS `aps-environment`, volontairement absente au MVP) avec une icône de notification monochrome pour Android.
- **Scan du ticket de caisse par IA** (voir la section dédiée).
- Compteur de gaspillage évité et d'argent économisé.
- Import des commandes drive.

### Idées notées pour plus tard

- Bouton rapide « −1 » sur les produits à la pièce (consommation partielle sans passer par l'édition).
- Prix d'achat des produits (nécessaire au compteur « argent économisé » de la V2, non stocké au MVP).
- Système d'amis hors foyer, pour voir et partager les recettes favorites de ses proches.
- Réglages des rappels de péremption dans l'app : interrupteur pour les activer ou désactiver, et choix de l'heure du résumé quotidien (aujourd'hui fixée à 18 h ; on les coupe dans les réglages du téléphone).

## À faire avant publication

Points volontairement reportés pendant le MVP, **bloquants pour une mise en production** :

- **Confirmation de l'adresse email** à l'inscription (nécessite un service d'envoi d'emails).
- **Mot de passe oublié** : réinitialisation par lien envoyé par email (même dépendance).
- **Énumération de comptes** : à revoir avec la confirmation d'email. Aujourd'hui, l'inscription renvoie 409 si l'email est pris et le message de verrouillage n'apparaît que pour un compte existant.
- **ForwardedHeaders** : codé, mais à configurer au déploiement : renseigner `ReverseProxy:KnownNetworks` (réseau du proxy, notation CIDR), sinon l'API voit l'IP du proxy pour tous les clients (rate limiting par IP faussé, logs inexacts).
- **Déploiement** (préparé, reporté à la fin du développement) : image Docker (`api/Dockerfile`), endpoint `/health` (API + base), migrations au démarrage (`Database:MigrateOnStartup`), redirection HTTPS laissée au proxy. Reste à faire : hébergement (Coolify envisagé), workflow GitHub Actions de construction de l'image, documentation du déploiement.
- **Nettoyage des refresh tokens** : tâche périodique qui supprime les jetons expirés ou révoqués.
- **Données de profil et IA** : indiquer dans la politique de confidentialité que les contraintes alimentaires (dont les allergies, avec consentement) sont transmises au fournisseur d'IA pour générer les recettes, sans identité ; prévoir l'export des données personnelles (droit d'accès).
- **Modèle d'IA** : Claude Haiku 4.5 (`claude-haiku-4-5-20251001`), retrait « pas avant le 15 octobre 2026 » (date minimale, pas un retrait programmé ; Anthropic prévient à l'avance). Avant publication, vérifier son statut sur la page Model deprecations d'Anthropic ; en cas d'annonce de retrait, changer `Ai:Model`.
- **Politique de confidentialité** : mentionner que les images de produits sont chargées directement depuis les serveurs d'Open Food Facts (qui voient donc l'adresse IP du téléphone), et que les données produit proviennent d'Open Food Facts (licence ODbL).
- **Politique de confidentialité, rappels de péremption** : indiquer que les notifications affichent des noms de produits, visibles sur l'écran verrouillé selon les réglages d'aperçu du téléphone (masquables dans les réglages de notification).

## Génération de recettes par IA

- Appel **uniquement depuis l'API**, derrière l'interface `IRecipeGenerator` (changer de fournisseur = une nouvelle implémentation). Deux modes, via `Ai:Provider` : `Fake` (recettes déterministes avec les vrais produits du foyer, par défaut en développement) et `Claude` (SDK officiel Anthropic, modèle dans `Ai:Model`).
- **Garde-fou** : l'API refuse de démarrer avec `Fake` hors de l'environnement Development, ou avec `Claude` sans clé hors Development.
- Format de sortie JSON imposé, puis **validation côté API** (bornes, références de produits) ; un refus du modèle, une réponse coupée ou invalide donnent une erreur 503, non décomptée du quota.
- **Favoris** : une étoile conserve une recette sans limite de durée. Les favoris forment le carnet de recettes commun du foyer (visibles par tous ses membres) ; 200 favoris maximum par utilisateur. Quitter le foyer retire ses étoiles ; supprimer son compte ne supprime pas les recettes du foyer (l'auteur devient anonyme).
- **Partage** : une recette se partage en texte via la feuille de partage native.
- Quotas : 3 générations par jour et par utilisateur, 50 par jour pour toute l'API (journée en heure de Paris). Historique conservé 30 jours, en ne stockant que la recette.
- Seuls les contraintes combinées et l'inventaire partent vers l'IA (aucune identité). Les noms de produits sont nettoyés et transmis comme données JSON (injection de prompt).

## Rappels de péremption (notifications)

- **Notifications locales** au MVP : le téléphone programme les rappels à partir de l'inventaire. Aucun changement côté API, aucune donnée personnelle en plus, et ça marche dans Expo Go.
- **Un résumé par jour à 18 h** (le moment où l'on se demande quoi cuisiner), qui suggère de demander une recette ; l'appui ouvre l'onglet Frigo.
- **DLC** : rappel la veille et le jour même. **DDM** : un seul rappel le jour même, au ton « à vérifier » (jamais « périmé »).
- Produits communs et produits perso de l'utilisateur ; jamais ceux des autres membres.
- **Horizon de 14 jours** : au plus 14 notifications programmées (iOS n'en garde que 64 par app).
- **Synchronisation idempotente** : à chaque chargement du Frigo, retour de l'app au premier plan, changement de foyer ou « J'ai cuisiné », l'app annule ses rappels puis les reprogramme (dans une file d'attente, pour éviter les doublons). Déconnexion, suppression du compte ou session expirée : rappels annulés, et ceux déjà affichés sont retirés (ils contiennent des noms de produits).
- **Autorisation** demandée une seule fois, après l'ajout d'un produit (pas au démarrage, où elle est souvent refusée par réflexe).

## Parcours à tester sur appareil

Étapes codées et vérifiées automatiquement (tests, typecheck, bundles) mais **pas encore testées sur un vrai téléphone**. Lancer l'environnement avec `.\dev.cmd`, puis cocher chaque parcours validé ; retirer une étape de la liste une fois tous ses parcours validés.

### Inventaire, étape 4 : liste du frigo et saisie manuelle

- [ ] Sans foyer, l'onglet Frigo renvoie vers l'onglet Foyer.
- [ ] Ajout d'un steak haché : la date proposée est le lendemain (DLC). Ajout de pâtes : DDM dans un an.
- [ ] Changement de la date avec « +3 j », puis avec le calendrier natif ; « Revenir à l'estimation » fonctionne.
- [ ] Quantité « 0,5 » en kg acceptée.
- [ ] Avec un second compte du foyer : un produit perso est visible mais en lecture seule, sans boutons Mangé / Jeté.
- [ ] « Mangé » et « Jeté » (avec confirmation) retirent le produit de la liste.
- [ ] Une DDM dépassée s'affiche « à vérifier », jamais « périmé ».

### Inventaire, étape 5 : scan du code-barres

- [ ] Premier scan : la demande d'accès à l'appareil photo s'affiche ; après un refus, l'écran propose « Autoriser » ou la saisie manuelle.
- [ ] Refus définitif : le bouton « Ouvrir les réglages » ouvre les réglages de l'app.
- [ ] Scan d'un produit connu (ex. Nutella 3017620422003) : écran de confirmation avec image, nom, marque, mention « Données Open Food Facts », formulaire pré-rempli.
- [ ] L'image n'apparaît nulle part ailleurs (liste du frigo, fiche du produit).
- [ ] Scan d'un produit inconnu d'OFF : message, formulaire vide avec code-barres conservé.
- [ ] API arrêtée pendant la recherche (ou OFF indisponible) : message « momentanément indisponible », saisie manuelle possible.
- [ ] Un QR code est ignoré (« Code non reconnu »), un code-barres alimentaire est lu du premier coup.
- [ ] Après « Ajouter au frigo », retour au frigo avec le produit dans la liste.

### Profil : onboarding et préférences

- [ ] Juste après l'inscription, l'onboarding s'affiche (5 questions, barre de progression) ; l'app n'est pas accessible avant la fin.
- [ ] Un compte existant sans profil passe aussi par l'onboarding à la connexion suivante.
- [ ] « Passer cette question » à chaque étape : l'onboarding se termine avec les valeurs par défaut.
- [ ] Allergies cochées sans consentement : message bloquant ; décocher le consentement efface les allergies cochées.
- [ ] Compte → « Mes préférences » : les réponses de l'onboarding sont reprises, modifiables et enregistrées.
- [ ] Foyer → « Équipement de la cuisine » : un changement fait par un membre est visible par un autre après rafraîchissement.

### Recettes par IA

- [ ] En développement (générateur Fake) : « Proposer une recette » affiche l'écran d'attente puis une recette qui utilise les produits qui périment en premier.
- [ ] Le compteur passe de 3 à 0 ; la 4e demande affiche « reviens demain » et le bouton est désactivé.
- [ ] Avec un colocataire végétarien sélectionné comme convive, la recette ne contient pas de viande.
- [ ] Bouton 🤖 (build de développement uniquement) : le prompt est copié dans le presse-papiers.
- [ ] « J'ai cuisiné cette recette » : les produits cochés disparaissent du frigo ; un produit perso d'un autre membre n'est pas cochable.
- [ ] Avec la vraie clé (`Ai__Provider=Claude`) : une recette est générée en moins d'une minute, en français, concise.
- [ ] API arrêtée pendant la génération : message d'erreur, le quota n'est pas décompté.
- [ ] Étoile sur une recette : elle apparaît dans « Favoris » pour tous les membres du foyer, avec le bon libellé (« Dans tes favoris », « Favori de 1 membre »).
- [ ] « Partager » ouvre la feuille de partage native avec la recette en texte lisible.

### Rappels de péremption (notifications locales)

- [ ] Aucune demande d'autorisation au démarrage ; elle apparaît après l'ajout d'un produit (manuel ou scanné), une seule fois, même après un refus. Dans Expo Go, l'autorisation est celle d'Expo Go : si elle a déjà été accordée ou refusée pour un autre projet, aucune demande n'apparaît (la réinitialiser en supprimant puis réinstallant Expo Go).
- [ ] Android 13+ : la demande s'affiche bien (canal « Produits à consommer » visible dans les réglages de notification de l'app).
- [ ] Bouton 🔔 du Frigo (build de développement uniquement) : le prochain résumé arrive 10 secondes plus tard, avec le même texte que celui de 18 h ; message « Aucun rappel prévu » si rien ne périme bientôt.
- [ ] Un produit en DLC qui périme demain, ajouté avant 18 h : notification à 18 h, « Ce soir : 1 produit à utiliser », avec « (demain) ».
- [ ] Une DDM du jour : « à vérifier », jamais « périmé ».
- [ ] Appui sur la notification, app fermée puis app en arrière-plan : l'onglet Frigo s'ouvre.
- [ ] Notification reçue app ouverte : le bandeau s'affiche quand même.
- [ ] Produit marqué « Consommé » avant 18 h : plus de notification pour lui.
- [ ] Produit perso d'un colocataire : n'apparaît pas dans mes rappels.
- [ ] Déconnexion : plus aucune notification ; les notifications déjà affichées de l'app disparaissent.
- [ ] Autorisation refusée : l'app fonctionne normalement, sans message d'erreur.

## Scan du ticket de caisse (V2)

Flux :
1. L'app prend la photo (appareil ou galerie, recadrage), la redimensionne à 1568 px sur le grand côté (résolution maximale lue par Haiku 4.5) et la compresse en JPEG.
2. L'app l'envoie à l'API .NET.
3. L'API appelle le modèle de vision avec un prompt et un **format de sortie JSON imposé**, derrière l'interface `IReceiptReader` (`Fake` en développement, `Claude` sinon, comme les recettes).
4. L'API valide la réponse et estime la date de péremption de chaque ligne à partir de sa catégorie.
5. L'app affiche **un écran de validation obligatoire** : l'utilisateur coche, corrige ou supprime les lignes avant l'ajout à l'inventaire (en une seule transaction).

Format de sortie imposé (catégories et unités en listes fermées, construites depuis la base) :

```json
{
  "purchaseDate": "YYYY-MM-DD ou null",
  "lines": [
    {
      "receiptText": "string (libellé brut du ticket)",
      "isFood": true,
      "name": "string (nom lisible)",
      "category": "code d'une catégorie de l'app (ex. ground-meat)",
      "quantity": 0,
      "unit": "Piece | Gram | Kilogram | Milliliter | Liter"
    }
  ]
}
```

Règles :
- **Ne jamais stocker l'image** après traitement (RGPD), ni la journaliser.
- **Minimisation** : seuls les articles et la date d'achat sont extraits (ni magasin, ni adresse, ni carte bancaire ou de fidélité).
- Validation côté API : lignes non alimentaires écartées, 60 lignes au plus, noms nettoyés, catégorie inconnue remplacée par « Autre », unité ou quantité invalide remplacée par 1 pièce, date d'achat future ou de plus de 30 jours remplacée par aujourd'hui. Une réponse inexploitable dans son ensemble (refus, coupée, illisible) donne une erreur 503, non décomptée du quota.
- Quotas : 3 lectures par jour et par utilisateur, 50 par jour pour toute l'API, séparés de ceux des recettes. Modèle réglable à part (`Ai:ReceiptModel`, Haiku 4.5 par défaut) : si la lecture de vrais tickets déçoit, on passe à un modèle plus précis sans toucher aux recettes.
- Tickets longs : les photographier en plusieurs fois (un scan par partie).

## Règles de sécurité (non négociables)

- **Aucune clé API dans l'app mobile.** Tout appel à l'IA ou à un service payant passe par l'API .NET.
- Secrets (clés IA, chaîne de connexion, clé JWT) dans des variables d'environnement ou les user-secrets .NET. Fichiers `.env` dans le `.gitignore` dès le premier commit.
- Chaque endpoint vérifie que l'utilisateur appartient bien au foyer dont il manipule les données.
- Limiter le nombre d'appels IA par utilisateur (coût).
- Ne stocker que les données personnelles nécessaires.

## Conventions

- Code (noms de classes, variables, méthodes) en **anglais** ; commentaires, messages de commit et documentation en **français**.
- Backend : architecture en couches claire (Controllers → Services → accès aux données), injection de dépendances, DTOs pour les entrées et sorties de l'API (jamais les entités EF directement).
- Mobile : TypeScript strict, composants fonctionnels et hooks, appels API centralisés dans un seul module.
- Tests unitaires sur la logique métier importante (estimation des dates, combinaison des profils, droits d'accès au foyer).

## Façon de travailler avec moi

- Je suis développeur en formation (alternance) : **explique les choix importants** et les concepts nouveaux, brièvement. Je dois comprendre et pouvoir défendre chaque partie du code.
- **Une fonctionnalité à la fois**, dans l'ordre du MVP ci-dessus. Propose un plan avant les gros morceaux.
- **Demande avant d'ajouter une dépendance** ou de changer un choix de la stack.
- Signale les problèmes de sécurité ou de conception que tu remarques, même si je ne les ai pas demandés.
- À la fin de chaque fonctionnalité : récapitule ce qui a été fait, comment le tester, et propose un message de commit.