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
3. Dans une nouvelle session, **P** : observer l'ouverture arrière, la montée et l'assise. Le passager doit rester à bord. Prendre le volant, démarrer et rouler : le passager suit le car ; **O** doit être refusé en mouvement. S'arrêter puis **O** : observer la descente et la fermeture. Relancer **P**, puis **O** pour vérifier la répétition.
4. Les commandes de revue du menu **Car Rapide > Vehicle** enregistrent des images et les contrôles dans `Library/VehicleReview`.

Les résultats datés figurent dans `VEHICLE_DRIVER_EXPERIENCE.md`. Les contrôles utilisent des enveloppes de membres contre les triangles d'origine (tolérance 1,5 cm) et une erreur de portée maximale de 5 cm. Ils complètent l'inspection visuelle ; ils ne constituent pas une collision exacte de chaque sommet du personnage. Les trajectoires sont calibrées pour les avatars et la scène fournis.

## Retour visuel et passager physique

- Posture debout redressée, balancement discret des bras pendant les pas, poignets libres alignés avec les avant-bras ; appui du chauffeur déplacé devant son épaule pendant l'entrée.
- Chauffeur en haut beige usé et jean délavé ; apprenti en haut marine à manches longues, short rouge à motifs blancs et bonnet. Références et provenance dans `WORKWEAR_REFERENCES.md`.
- Vitrages latéraux de cabine retirés des deux portes avant ; pare-brise conservé.
- Commandes distinctes **P/O** et état assis persistant pendant le trajet.
- Passager à pied doté de collisions et d'un corps articulé qui tombe sous un choc du véhicule. Pas de récupération automatique après la chute.

Recette supplémentaire : en session neuve, reculer vers le passager resté dehors. À faible vitesse le contact doit rester solide ; à vitesse supérieure, le passager doit tomber et rester soumis aux collisions du sol et du car. Vérifier également le bonnet et la continuité des manches de l'apprenti en vue arrière.

## Validation finale — 30 septembre 2026

Le chauffeur a passé les **16 contrôles** de la revue vidéo du 28 septembre. La revue détaillée du passager du 30 septembre passe **4 contrôles sur 4**, avec une erreur de portée maximale de **2,9 cm** et un chevauchement maximal des enveloppes de **0,8 cm** (tolérance : 1,5 cm). Le recul de 8 cm du pied droit intermédiaire corrige le contact avec le bord de la plateforme pendant la descente.

Le test complet de trajet et collision passe **21 contrôles sur 21** : vitres avant ouvertes et métal conservé, assise persistante, suivi du véhicule en virage, refus de descendre en mouvement, deux descentes avec remontée après changement de cap, contact solide à basse vitesse, puis chute physique sous un choc à 14,4 km/h. La capture finale montre le passager au sol derrière le car. Les résultats et images sont dans `Recordings/VehicleRealism-2026-09-30/` (hors Git).

Unity a redémarré et compilé normalement le 30 septembre ; le blocage de licence du 29 septembre n'empêche plus cette recette. Les limites restent celles de la scène et des avatars fournis, avec des trajectoires procédurales calibrées et des enveloppes de collision approximatives.
