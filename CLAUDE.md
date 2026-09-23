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
- **Notifications** : push via Expo Notifications.

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
- **Profil** : temps de cuisine souhaité, budget par repas, équipement disponible (plaques, four, air fryer, micro-ondes…), régime et allergies, objectif (équilibré, prise de muscle, anti-gaspi simple…), nombre de portions par défaut.
- **Repas partagé** : quand plusieurs membres mangent ensemble, les contraintes de leurs profils se combinent (si l'un est végétarien, la recette l'est ; les allergies de tous sont exclues).

### Estimation des dates de péremption

Chaque catégorie de produit a une durée de conservation par défaut (ex. viande fraîche ≈ 3 jours, yaourt ≈ 3 semaines). La date est estimée à l'ajout ; l'utilisateur peut la corriger. Les durées sont stockées en base (table de référence), pas en dur dans le code.

## Périmètre du MVP (ordre de développement)

1. **Comptes et foyers** : inscription, connexion, création d'un foyer, invitation de membres.
2. **Inventaire** : ajout par code-barres (Open Food Facts) et par saisie manuelle, dates de péremption estimées, modification et suppression, marquer un produit comme consommé ou jeté.
3. **Onboarding et profil** : 4 à 5 questions à l'inscription, modifiables ensuite.
4. **Suggestions de recettes par IA** : priorité aux produits qui périment en premier, respect des contraintes du ou des profils.
5. **Notifications** : alerte avant péremption.

### V2 (après le MVP)

- **Scan du ticket de caisse par IA** (voir la section dédiée).
- Compteur de gaspillage évité et d'argent économisé.
- Import des commandes drive.

## À faire avant publication

Points volontairement reportés pendant le MVP, **bloquants pour une mise en production** :

- **Confirmation de l'adresse email** à l'inscription (nécessite un service d'envoi d'emails).
- **Mot de passe oublié** : réinitialisation par lien envoyé par email (même dépendance).
- **Énumération de comptes** : à revoir avec la confirmation d'email. Aujourd'hui, l'inscription renvoie 409 si l'email est pris et le message de verrouillage n'apparaît que pour un compte existant.
- **ForwardedHeaders** : à activer derrière le reverse proxy, sinon l'API voit l'IP du proxy pour tous les clients (rate limiting par IP faussé, logs inexacts).
- **Nettoyage des refresh tokens** : tâche périodique qui supprime les jetons expirés ou révoqués.

## Scan du ticket de caisse (V2)

Flux :
1. L'app prend la photo et la compresse.
2. L'app l'envoie à l'API .NET.
3. L'API appelle le modèle de vision avec un prompt et un **format de sortie JSON imposé**.
4. L'API mappe chaque catégorie vers une durée de conservation estimée.
5. L'app affiche **un écran de validation obligatoire** : l'utilisateur coche, corrige ou supprime les lignes avant l'ajout à l'inventaire.

Format de sortie attendu :

```json
{
  "enseigne": "string",
  "date_achat": "YYYY-MM-DD",
  "produits": [
    {
      "libelle_ticket": "string (texte brut du ticket)",
      "nom_normalise": "string (nom lisible)",
      "categorie": "string (catégorie connue de l'app)",
      "quantite": 0,
      "unite": "string",
      "alimentaire": true
    }
  ]
}
```

Règles : ignorer les produits non alimentaires, valider le JSON côté API avant de l'utiliser, **ne jamais stocker l'image** après traitement (RGPD).

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