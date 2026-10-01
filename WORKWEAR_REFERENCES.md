# Tenues de travail — 28 septembre 2026

Les trois photos fournies par l'utilisateur servent de références de vêtements et d'ouvertures de cabine. Elles ne sont pas redistribuées dans le dépôt et le visage d'aucune personne photographiée n'est reproduit.

- Chauffeur : polo beige usé, jean bleu-gris délavé, chaussures foncées. Texture : `Assets/Resources/CarRapide/Characters/Workwear/DriverWorkwear.png`.
- Apprenti : haut bleu marine, manches longues, short rouge à motifs blancs et bonnet sombre. Texture : `Assets/Resources/CarRapide/Characters/Workwear/ReceiverWorkwear.png`. Les manches et le bonnet sont des maillages ajoutés au squelette dans `WorkwearAppearance`.
- Le passager garde sa tenue indépendante.
- Les deux portes avant utilisent des copies du maillage sans les triangles du vitrage, avec conservation de la porte métallique et du pare-brise. Les sous-maillages sont identifiés par matériau : leur ordre diffère entre gauche et droite.

Ces tenues sont des adaptations des avatars VALID sous licence MIT, inspirées des références ; elles ne constituent pas une reconstitution exacte des photos. Attribution et licence : `CREDITS.md` et `ThirdParty/VALID-LICENSE.txt`.

## Génération des textures

Outil intégré `image_gen`, en mode édition d'image, avec les atlas UV originaux de `Black_M_1_Casual` et `Black_M_2_Casual` comme sources. Pas de CLI externe. Les deux textures générées ont été enregistrées aux chemins ci-dessus puis appliquées et inspectées sur les avatars dans Unity.

Prompt chauffeur :

> Use case: precise-object-edit. This is a production 2048x2048 UV texture atlas for an existing 3D game character. Edit only the clothing color/detail islands. Absolutely preserve the layout, borders, positions, scale, orientation, skin, face, hair, eyes, teeth, hands and all non-clothing pixels. Do NOT create a portrait or a character rendering. Keep a flat square texture atlas. Change the pale blue shirt fabric islands (large lower right torso, two sleeve cuffs, white collar around neck) into a weathered warm beige/tan cotton polo-like work shirt, subtle mottled wear, no chest pockets or full-length button row; keep a short collar opening at the neck. Change the big taupe trousers island in the lower centre into worn faded blue-grey denim jeans with subtle seams and dusty worn knees, preserve its exact UV silhouette. Shoes become understated scuffed dark brown. No logo, no extra text, no watermark. All original skin/hair islands unchanged. Output only the texture atlas, identical UV layout.

Prompt apprenti :

> Use case: precise-object-edit. Edit this square UV atlas for a 3D game character's clothes ONLY. Preserve every UV island boundary, position, size, orientation and silhouette EXACTLY. Output only a flat square UV texture atlas, not a person or a scene. Keep face, hands, exposed legs, eyes, mouth, teeth and all skin unchanged. Change the white shorts island at lower centre into deep red cotton shorts with scattered small white floral/leaf motifs, similar to everyday Senegalese car rapide apprentice workwear. Keep the dark navy torso islands at lower right but make the cloth look softly worn with a narrow faded cream stripe across upper chest and upper sleeves; no logos. Make the two narrow sleeve cuff islands navy. Keep shoe islands dark brown, discreet and worn. Maintain original flat albedo lighting and UV placement with no new UV islands or objects. No text, no watermark.
