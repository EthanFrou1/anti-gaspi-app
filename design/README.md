# Design

Fichiers de conception, **non utilisés par l'app** (ils ne sont pas inclus dans le build mobile).

## `leftly/` : charte graphique Leftly

Livrable de la charte, avec son arborescence d'origine (voir [leftly/README.md](leftly/README.md)) :
tokens de référence, sources SVG des icônes d'app et du splash, aperçus, icône du Play Store,
logos et illustrations pas encore utilisés.

Ce que l'app utilise a été **déplacé** (pas copié) dans `mobile/assets/brand/`, avec la même
arborescence : icônes d'app, logo du splash, icônes d'état et de catégorie, symbole (favicon web).
Quand un écran se sert d'un nouveau fichier (une illustration par exemple), on le déplace d'ici
vers `mobile/assets/brand/`.

Les tokens utilisés par l'app sont une copie de `leftly/leftly-tokens.ts` dans
`mobile/src/theme/tokens.ts` (une seule correction de typage, décrite en tête du fichier).
