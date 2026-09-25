# Lecture des tickets par l'IA : résultats de l'évaluation

Synthèse des mesures du 25/09/2026 avec `evaluate.py`, sur les tickets fictifs de ce dossier. Ce fichier est écrit à la main (contrairement au `README.md`, généré par `generate.py`).

## Protocole

- **Données** : 5 tickets générés, soit 7 images (le ticket long entier, puis en deux parties). Au total 100 lignes alimentaires à lire et 9 lignes non alimentaires à écarter.
- **Modèle** : Claude Haiku 4.5 (`claude-haiku-4-5-20251001`), appelé par l'API locale en mode `Claude`, avec les images préparées comme dans l'app (1568 px, JPEG qualité 70).
- **Mesure** : 3 passages par version. Le modèle ne répond pas toujours pareil : une même ligne peut être juste à un passage et fausse au suivant, donc un seul passage ne prouve rien.
- **Critère fixé avant de voir les chiffres** : moyenne des quantités justes supérieure à celle du prompt d'origine, aucun passage en dessous, et aucune baisse ailleurs (produits trouvés, catégories, lignes non alimentaires, dates).

## Les trois versions

| Version | Commit | Principe |
|---|---|---|
| **A. Prompt d'origine** | `aa1d5d7` | Le modèle écrit directement la quantité totale. |
| **B. Exemplaires lus, total calculé** | `67f4be7` | Le modèle donne le nombre d'exemplaires et le contenu d'un exemplaire ; l'API multiplie. |
| **C. Vérification par les prix, lot, centilitres** | `3e3153f` | Le modèle recopie aussi les prix, le lot du libellé et les centilitres ; l'API vérifie et calcule tout. |

Un essai intermédiaire (`ad2d24d`) ajoutait seulement une règle sur les multiplicateurs dans le prompt. Il a été mesuré deux fois (94 et 92 quantités justes) : la nouvelle consigne faisait lire le code TVA comme une quantité sur des lignes simples. Il a été remplacé par la version B.

## Résultats

| | A. Origine | B. Exemplaires | C. Prix, lot, cl |
|---|---|---|---|
| **Quantités justes / 100** | 92 · 93 · 91 (**92,0**) | 95 · 94 · 95 (**94,7**) | 97 · 99 · 99 (**98,3**) |
| Produits trouvés, sans invention | 100 % | 100 % | 100 % |
| Erreurs de catégorie (3 passages) | 2 | 2 | 3 |
| Non alimentaires écartés, dates d'achat | 100 % | 100 % | 100 % |
| Tokens par passage (entrée / sortie) | 21 526 / 5 153 | 23 325 / 5 788 | 25 481 / 7 908 |
| **Coût moyen par lecture** | 0,68 centime | 0,75 centime | **0,93 centime** |
| **Durée moyenne (max) par image** | 6,8 s (9,5 s) | 7,6 s (10,3 s) | **10,0 s (14,7 s)** |

Les erreurs de catégorie sont du bruit, sans lien avec les versions : la mozzarella classée `soft-cheese` au lieu de `fresh-cheese`, et une fois les lentilles en `canned`. Coûts calculés avec les tarifs de Haiku 4.5 (1 $ / 5 $ par million de tokens en entrée / sortie).

### Erreurs de quantité par type (total des 3 passages)

| Type d'erreur | A | B | C |
|---|---|---|---|
| Multiplicateur ignoré (« 6 x 0,99 » sous le libellé) | 24 | 0 | 0 |
| Code TVA lu comme un nombre d'exemplaires (« 4,50 € 2 ») | 0 | 3 | 0 |
| Lot du libellé non compté (« 6X1,5L », « 6X25CL ») | 0 | 6 | 0 |
| Centilitres pris pour des millilitres (« 20CL » → 20 ml) | 0 | 4 | 0 |
| Lot « X2 » en pièces perdu (« STEAK HACHE X2 » → 1 pièce) | 0 | 3 | 5 |
| **Total** | **24** | **16** | **5** |

## Enseignements

1. **Le modèle lit, le code calcule.** Haiku lit correctement les nombres imprimés, mais pas le calcul qu'il doit en faire dans sa réponse. Même avec la règle écrite en toutes lettres, le yaourt « X4 » acheté 3 fois donnait 4 pièces aux trois passages de la version A. La multiplication, le lot et la conversion des centilitres sont maintenant faits par `ReceiptValidator` : un code déterministe, testé unitairement, qui donne 12 pièces à chaque fois.
2. **Une consigne peut créer une erreur ailleurs.** Chaque correction du prompt a déplacé une partie des erreurs vers des lignes qui étaient justes :
   - « colonne de quantité » a fait lire le code TVA comme une quantité ;
   - « ne multiplie pas » a fait perdre les lots écrits dans le libellé ;
   - l'exemple « 75CL », donné sans son unité, a fait écrire « 75 Milliliter ».

   Il faut mesurer toutes les lignes, pas seulement celles qu'on cherche à corriger.
3. **Vérifier par deux lectures indépendantes.** Le nombre d'exemplaires n'est retenu que si `exemplaires × prix unitaire ≈ prix de la ligne`. Sinon, un rapport entier entre les deux prix le remplace, et à défaut il vaut 1. Un code TVA n'est jamais accompagné d'un prix unitaire : ces erreurs ont disparu. En cas de doute, on revient au comportement le plus prudent : 1 exemplaire, que l'utilisateur corrige.
4. **La précision a un prix.** Les champs lus en plus (prix, lot) augmentent la sortie de 54 %, soit +37 % de coût et +3 s par lecture. À 50 lectures par jour au maximum pour toute l'API, le coût reste d'environ 0,47 $ par jour.
5. **Minimisation.** Les prix servent uniquement à la vérification : ils ne sont ni renvoyés à l'app, ni enregistrés, ni journalisés. Un test de bout en bout vérifie qu'ils ne sont pas renvoyés.

## Limites

- **Tickets générés** : une seule police, une image propre, des enseignes inventées, et un prix unitaire imprimé sur chaque ligne multipliée. Les vrais tickets diront si toutes les enseignes impriment ce prix unitaire. Sinon, l'article retombe à 1 exemplaire.
- **3 passages par version**, c'est un petit échantillon : un écart de 1 ou 2 points n'est pas significatif. Les écarts mesurés ici (24, 16, puis 5 erreurs) le sont.
- **On arrête d'optimiser sur ces tickets.** Au-delà, on risquerait d'adapter le prompt à ces exemples plutôt qu'aux tickets réels. La suite, ce sont les vrais tickets (parcours de CLAUDE.md).

## Piste pour plus tard : le lot « X2 » en pièces

Les 5 erreurs restantes de la version C ont la même forme : un lot en pièces (« STEAK HACHE 15% X2 », « FILET PLT X2 ») est lu avec `packSize` à 1 et `quantity` à 1 pièce. Cause probable : la règle 4 du prompt donne en exemple « STEAK HACHE 5%MG X2 » → « Steak haché 5 % MG », ce qui apprend peut-être au modèle à traiter « X2 » comme du bruit. Deux essais possibles : un exemple de nom sans lot, ou une mention explicite (« X2 » donne `packSize` 2). À confirmer d'abord sur de vrais tickets, avant toute modification.

## Refaire la mesure

```
.\dev.cmd -Claude -ReceiptQuota 30
python design/test-tickets/evaluate.py --save resultat.json
```

Chaque passage lit 7 images, pour environ 0,065 $ avec la version C. Au-delà de 50 lectures dans la journée, relever aussi la limite globale de l'API (`Ai__ReceiptGlobalDailyLimit`, variable d'environnement de la fenêtre de l'API).
