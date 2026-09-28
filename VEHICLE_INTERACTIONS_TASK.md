# Interactions réalistes avec le Car Rapide

Objectif : corriger les traversées de carrosserie, de portes et de sièges pendant les séquences du chauffeur et du passager, et rendre les transitions plus continues.

## Changements

- Chauffeur : ouverture avec retrait du bras, appui devant le passage de roue, entrée sous le cadre, pivot continu autour de la colonne de direction, assise recalée sur le coussin et fermeture depuis l'intérieur.
- Porte chauffeur : contrôle du volume du corps pendant toute la fermeture, avec arrêt si le passage est obstrué ; le moteur reste verrouillé jusqu'à la fermeture.
- Passager : attente à côté du débattement de la porte arrière, prise extérieure puis intérieure, déplacement vers l'allée après ouverture, sortie avec déplacement latéral avant fermeture.
- Animation : appuis fixes, pieds levés avant les seuils, continuité du bassin entre les pas, orientation des genoux et coudes ; les anciennes animations enregistrées ne s'ajoutent plus à la pose corporelle corrigée.
- Géométrie : conservation des maillages d'origine ; volant déplacé de 10 cm vers l'avant et de 5 cm vers le haut dans la scène, repères de mains et de siège recalibrés.

## Recette

Dans Unity 6000.6.1f1, ouvrir `Assets/Scenes/SampleScene.unity` et lancer Play.

1. **E** : observer le chauffeur de l'approche jusqu'à la fermeture ; vérifier les pieds, les genoux et le bras pendant l'ouverture.
2. **R**, puis **W/A/S/D** et **Espace** : vérifier le contact, le son moteur et le verrouillage des commandes avant démarrage.
3. Dans une nouvelle session, **P** : observer l'ouverture arrière, la montée, l'assise, la descente et la fermeture ; relancer **P** pour vérifier la répétition.
4. Les commandes de revue du menu **Car Rapide > Vehicle** enregistrent des images et les contrôles dans `Library/VehicleReview`.

Les résultats datés figurent dans `VEHICLE_DRIVER_EXPERIENCE.md`. Les contrôles utilisent des enveloppes de membres contre les triangles d'origine (tolérance 1,5 cm) et une erreur de portée maximale de 5 cm. Ils complètent l'inspection visuelle ; ils ne constituent pas une collision exacte de chaque sommet du personnage. Les trajectoires sont calibrées pour les avatars et la scène fournis.
