# AudioBlocks — Guide de Présentation PowerPoint

> Ce guide te donne tout ce dont tu as besoin pour préparer une présentation claire et convaincante du projet AudioBlocks.
> Plan de 18 slides avec contenus, idées de visuels, analogies et conseils de présentation.

---

## Conseils généraux avant de commencer

- **Durée cible** : 15-20 minutes de présentation + 5-10 minutes de questions
- **Police recommandée** : Grande (28+ pt pour le corps, 40+ pt pour les titres) — la salle est souvent grande
- **Couleurs suggérées** : Fond sombre (noir ou bleu nuit) + texte clair + accents en orange/jaune électrique — ça évoque l'audio et la scène
- **Images** : Préfère des captures d'écran de l'app réelle plutôt que des mockups génériques
- **Démo live** : Prévois deux moments de démo (slide 3 et slide 16) — teste l'app AVANT sur le PC de présentation
- **Tip général** : Une idée par slide. Si tu dois lire ton slide, il y a trop de texte.

---

## Plan des slides

---

### Slide 1 — Titre

**Titre** : `AudioBlocks`
**Sous-titre** : `Processeur d'effets audio en temps réel`
**Auteur + date + contexte** (ex. : CFC Informatique — Mars 2026)

**Visuel suggéré** :
- Grande image de fond : une rangée de pédales d'effets physiques (photo libre de droits) OU une capture d'écran de l'app en grand plan
- Logo de l'app si disponible
- Couleur de fond : noir avec titre en blanc et accents orange

**Conseil** : Reste sur cette slide pendant que les gens s'installent. Commence par une anecdote ou une question au public : *"Vous avez déjà entendu une guitare électrique avec de la reverb ?"*

---

### Slide 2 — "C'est quoi AudioBlocks ?"

**Titre** : `AudioBlocks — Le pedalboard virtuel`

**Contenu suggéré** :
- 1 phrase pitch : *"AudioBlocks transforme ton ordinateur en pedalboard de guitariste professionnel."*
- 3 bullet points :
  - 🎙️ Son entre depuis le micro / la guitare
  - 🎛️ Traverse une chaîne d'effets configurable
  - 🔊 Sort dans les haut-parleurs en temps réel

**Visuel suggéré** :
- Côté gauche : photo d'un vrai pedalboard physique (avec câbles et pédales)
- Côté droit : capture d'écran de l'interface AudioBlocks
- Flèche entre les deux : *"→ Remplacé par :"*

**Point de discussion** : *"Pourquoi faire ça en logiciel ?"* → Pas de matériel à acheter, tout est configurable, tout est sauvegardable.

---

### Slide 3 — Démo rapide (1ère démo live)

**Titre** : `Voyons ça en action`

**Contenu suggéré** :
- Texte minimal — juste *"Démo live"* en grand
- En dessous (petites notes pour toi) :
  - Brancher le micro
  - Activer Distortion + Reverb
  - Parler ou chanter dans le micro
  - Montrer le VU-mètre qui réagit

**Conseil de présentation** :
- ⚠️ Teste TOUJOURS l'audio avant la présentation sur le PC de présentation
- Prévoir un plan B : si l'audio ne fonctionne pas, avoir une vidéo de démonstration pré-enregistrée
- Garde cette démo courte (1-2 minutes) — l'effet "wow" doit venir tôt

---

### Slide 4 — Le problème qu'on résout

**Titre** : `Pourquoi ce projet ?`

**Contenu suggéré** :
- Un vrai pedalboard physique : 300-2000 CHF, encombrant, pas flexible
- Les logiciels pro (Guitar Rig, Amplitube) : coûteux, complexes, propriétaires
- AudioBlocks : **gratuit, open-source, léger, personnalisable**

**Visuel suggéré** :
- Tableau comparatif à 3 colonnes :

| | Pedalboard physique | Logiciel pro | AudioBlocks |
|---|---|---|---|
| Prix | 300-2000 CHF | 50-200 CHF/an | Gratuit |
| Flexibilité | Fixe | Bonne | Totale |
| Complexité | Moyenne | Haute | Accessible |

**Point de discussion** : *"C'est un projet pédagogique — l'objectif n'est pas de concurrencer Guitar Rig, mais de comprendre comment ça fonctionne de l'intérieur."*

---

### Slide 5 — Technologies choisies

**Titre** : `La stack technique`

**Contenu suggéré** (logo + 1 mot par techno) :
- **C# / .NET 8** → Le langage
- **Avalonia UI** → L'interface (cross-platform)
- **NAudio** → Le son
- **WASAPI / ASIO** → La carte son
- **JSON** → Les presets

**Visuel suggéré** :
- 5 rectangles arrondis disposés horizontalement, chacun avec le nom de la techno + une icône simple
- Couleurs : chaque rectangle dans une couleur différente mais harmoniante (bleu, vert, orange, violet, gris)

**Conseil** : Ne pas entrer dans les détails techniques ici — juste montrer qu'on a réfléchi aux choix. Les détails viendront plus tard.

**Point de discussion** : *"Pourquoi Avalonia et pas WinForms ?"* → Cross-platform (Windows/Mac/Linux) et moderne.

---

### Slide 6 — Architecture globale

**Titre** : `Comment le son circule`

**SCHÉMA À DESSINER dans PowerPoint** :
```
4 rectangles en ligne avec flèches entre eux :

┌──────────────┐     ┌──────────────┐     ┌──────────────────────┐     ┌──────────────┐
│   Entrée     │────▶│    Moteur    │────▶│   Chaîne d'effets    │────▶│   Sortie     │
│  Micro /     │     │   Audio      │     │  FX1 → FX2 → FX3    │     │  Haut-       │
│  Guitare     │     │              │     │                      │     │  parleurs    │
└──────────────┘     └──────────────┘     └──────────────────────┘     └──────────────┘
```

**Instructions pour PowerPoint** :
- 4 rectangles avec coins arrondis, fond bleu foncé, texte blanc
- Flèches épaisses (4-5 pt) en orange ou blanc entre chaque rectangle
- Icônes : 🎙️ sur Entrée, ⚙️ sur Moteur, 🎸 sur Effets, 🔊 sur Sortie
- Fond de slide : noir ou bleu très foncé

**Analogie à dire** : *"C'est comme une chaîne de montage — chaque station reçoit le produit, fait son travail, et le passe à la suivante."*

---

### Slide 7 — Le moteur audio

**Titre** : `Le cœur du système — AudioEngine`

**SCHÉMA À DESSINER** :
```
Schéma "boucle de callback" :

┌────────────────────────────────────────────────────────────────┐
│                         Thread Audio                           │
│                                                                │
│   Carte son ──────────▶ "Donne-moi 256 samples !" ──────────▶ │
│       │                                                  │     │
│       │         ┌──────────────────────────┐             │     │
│       └────────▶│    AudioEngine.Read()    │─────────────┘     │
│                 │  1. Récupère son brut     │                   │
│                 │  2. Passe aux effets      │                   │
│                 │  3. Retourne son modifié  │                   │
│                 └──────────────────────────┘                   │
└────────────────────────────────────────────────────────────────┘
```

**Instructions pour PowerPoint** :
- Un grand rectangle en pointillés = le "Thread Audio"
- Dedans : un rectangle central "AudioEngine.Read()" avec 3 étapes numérotées
- Flèche circulaire autour qui montre que ça tourne en boucle (en continu)
- Couleur : fond du thread en bleu nuit, flèches en orange

**Analogie à dire** : *"C'est comme un chef cuisinier qui reçoit des commandes toutes les 5 ms et doit servir dans les temps — sinon le client entend des craquements."*

**Point de discussion** : *"Pourquoi un thread dédié ?"* → Si le thread audio est bloqué par l'UI (ex. tu ouvres une fenêtre), on entend des coupures.

---

### Slide 8 — Les effets — Vue d'ensemble

**Titre** : `La chaîne d'effets`

**SCHÉMA À DESSINER** :
```
Chaîne de pédales (style guitare) :

[Noise Gate] ──▶ [Gain] ──▶ [Distortion] ──▶ [EQ] ──▶ [Reverb] ──▶ [Delay]
    🚪              🔊           🔥              🎚️          ⛪          🏔️
```

**Instructions pour PowerPoint** :
- 6 rectangles de taille identique, disposés horizontalement (ou en serpentin sur 2 lignes si 10 effets)
- Chaque rectangle : fond coloré différent (gradient), nom de l'effet + icône emoji
- Flèches entre chaque rectangle
- Les effets peuvent être glissés/déposés pour changer l'ordre → note-le sous le schéma

**Contenu textuel** :
- *"10 effets disponibles, activables/désactivables indépendamment"*
- *"Ordre modifiable par drag & drop"*
- *"Chaque effet a ses propres paramètres"*

---

### Slide 9 — Focus : Distortion vs Fuzz

**Titre** : `Distortion et Fuzz — La même idée, deux résultats différents`

**SCHÉMA À DESSINER** (Courbes de clipping) :

```
Deux graphiques côte à côte :

Distortion (Soft Clipping)          Fuzz (Hard Clipping)
Signal en entrée : /\/\/\           Signal en entrée : /\/\/\

Sortie :  ╭───╮                     Sortie : ┌───┐
         /     \                            /     \
────────/       \────────          ─────────       ─────────
        \       /                          \       /
         ╰───╯                              └───┘
(Arrondi, doux)                     (Coupé net, agressif)
```

**Instructions pour PowerPoint** :
- Deux graphiques dans des encadrés séparés
- Axe X = temps, Axe Y = amplitude du son
- Distortion : courbe lisse, arrondie aux sommets → couleur orange/chaude
- Fuzz : courbe avec "plateaux" plats aux sommets → couleur rouge/agressive
- Utilise l'outil "Formes" de PowerPoint + courbes Bézier pour les dessiner

**Analogie à dire** :
- Distortion = *"On arrondit les angles comme pour polir une pierre"*
- Fuzz = *"On coupe net comme avec des ciseaux"*

---

### Slide 10 — Focus : Reverb

**Titre** : `La Reverb — Simuler une salle`

**SCHÉMA À DESSINER** (Algorithme Freeverb simplifié) :

```
                    ┌─────────────────────────────────────┐
                    │           FREEVERB                  │
Son sec ──▶ [Mix] ──▶│  Comb1 ─┐                          │──▶ Son avec reverb
     │              │  Comb2 ─┤                          │
     │              │  Comb3 ─┤─▶ [Mélange] ─▶ [AllPass] │
     │              │  Comb4 ─┘                          │
     │              └─────────────────────────────────────┘
     │                                        │
     └──────────────── Mix Dry/Wet ───────────┘
```

**Instructions pour PowerPoint** :
- Rectangle principal = boîte Freeverb
- Dedans : 4 petits rectangles "Comb" empilés → flèche vers "Mélange" → flèche vers "AllPass"
- Flèches extérieures montrant le signal sec (Dry) qui bypass et se remélange à la fin
- Couleurs : fond bleu/violet pour l'espace/profondeur

**Analogie à dire** : *"Les 'Comb filters' sont comme plusieurs salles parallèles — le son rebondit dans chacune à une vitesse différente, puis on mélange tous les rebonds."*

---

### Slide 11 — Focus : Compresseur

**Titre** : `Le Compresseur — Dompter le volume`

**SCHÉMA À DESSINER** (Avant/Après) :

```
Avant compression :               Après compression :
                                                        
Volume ▲                          Volume ▲         
       │  █                              │   ╔═╗    
       │  █                              │   ║ ║    
       │  █   █                          │   ║ ║ ╔═╗
       │█ █ █ █ █                        │╔═╗║ ║ ║ ║╔
───────┼──────────▶ Temps         ───────┼──────────▶ Temps
   (Dynamique large)                  (Dynamique réduite)
```

**Instructions pour PowerPoint** :
- Deux "histogrammes" simplifiés côte à côte
- Gauche : barres de hauteurs très variées (dynamique large)
- Droite : barres plus uniformes (dynamique réduite)
- Une ligne rouge horizontale = le seuil (threshold)
- Annotation : *"Ce qui dépasse le seuil est réduit"*

**Analogie à dire** : *"Un technicien son avec un doigt sur le bouton de volume — il baisse dès que c'est trop fort."*

---

### Slide 12 — L'interface utilisateur

**Titre** : `Les contrôles — Inspirés du matériel physique`

**Contenu suggéré** :
- Capture d'écran de l'interface principale annotée avec des flèches :
  - → Knob (potentiomètre rotatif)
  - → Fader (curseur linéaire)
  - → VU-mètre (vert / orange / rouge)
  - → Chaîne d'effets (drag & drop)

**SCHÉMA À DESSINER** (si pas de capture dispo) :

```
┌─────────────────────────────────────────────────────┐
│                  AudioBlocks                        │
│  ┌────────────────────────────────────────────────┐ │
│  │  [Noise Gate] [Gain] [Distortion] [Reverb]     │ │
│  │     🎚️         🎚️        🎚️          🎚️       │ │
│  └────────────────────────────────────────────────┘ │
│                                                     │
│  Volume Master : ████████░░  🔊                     │
│                                                     │
│  VU-mètre : ██████████▓▓░░░░  [-12 dB]              │
│                                                     │
│  [▶ Play] [⏹ Stop] [⏺ Enregistrer] [💾 Preset]     │
└─────────────────────────────────────────────────────┘
```

**Conseil** : Utilise une vraie capture d'écran de l'app si possible — bien plus percutant qu'un dessin.

---

### Slide 13 — Système de presets

**Titre** : `Les presets — Sauvegarder ses réglages`

**SCHÉMA À DESSINER** (Flux Save/Load) :

```
SAUVEGARDER :

[Réglages actuels] ──▶ [PresetManager] ──▶ [Fichier JSON]
  (effets + params)     .SavePreset()        preset.json
  
CHARGER :

[Fichier JSON] ──▶ [PresetManager] ──▶ [Moteur audio]
  preset.json        .LoadPreset()      (reconfigure les effets)
```

**Instructions pour PowerPoint** :
- Deux flux séparés (Sauvegarder / Charger) avec des couleurs différentes
- Sauvegarder : flèches en bleu (gauche → droite)
- Charger : flèches en vert (gauche → droite, mais dans l'autre sens logique)
- Icône de fichier JSON au centre (rectangle avec coins cornés)

**Contenu textuel** :
- *"Format JSON — lisible, partageable, versionnable (Git)"*
- *"Contient la liste des effets + tous leurs paramètres"*
- *"Chargement immédiat — zéro latence"*

---

### Slide 14 — Enregistrement et export WAV

**Titre** : `L'enregistreur — Capturer le son traité`

**SCHÉMA À DESSINER** :

```
Pendant l'enregistrement :

Thread audio ──▶ [Son traité] ──▶ [Buffer en mémoire]
                                       (liste de nombres)

À l'export :

[Buffer en mémoire] ──▶ [AudioRecorder] ──▶ [Fichier WAV]
   (tous les samples)     .ExportWav()       sortie.wav
```

**Contenu textuel** :
- *"Le son est capturé APRÈS les effets — tu enregistres ce que tu entends"*
- *"Stocké en mémoire RAM pendant l'enregistrement"*
- *"Exporté en WAV standard (lisible par tout)"*
- *"Thread-safe : le thread audio et l'UI ne se marchent pas dessus"*

**Analogie à dire** : *"C'est comme un magnétophone branché à la fin de la chaîne."*

---

### Slide 15 — Défis techniques rencontrés

**Titre** : `Les défis — Ce qui était vraiment difficile`

**Contenu suggéré** (3-4 défis, chacun avec cause + solution) :

| Défi | Cause | Solution |
|---|---|---|
| **Latence** | Thread audio en compétition avec l'UI | Thread dédié haute priorité |
| **Thread-safety** | Audio thread + UI thread modifient les mêmes données | Locks + opérations atomiques |
| **Précision du métronome** | Arrondi des temps en entiers | Accumulateur en `double` |
| **Performance** | 10 effets × 48 000 samples/sec | Optimisation des boucles, pas d'allocation en temps réel |

**Conseil de présentation** : Cette slide montre ta maturité technique. Parle de ce que TU as appris, pas juste des solutions théoriques.

**Point de discussion** : *"Comment tu as débogué les problèmes de thread ?"* → Difficile à reproduire (race conditions), utilisation de logs + tests de charge.

---

### Slide 16 — Démo finale (2ème démo live)

**Titre** : `Démo complète`

**Contenu minimal** — Juste "Démo live" + liste de ce que tu vas montrer :
1. Activer plusieurs effets
2. Modifier les paramètres en temps réel (knobs)
3. Enregistrer un extrait
4. Sauvegarder un preset
5. Charger un preset différent

**Conseil de présentation** :
- Prévois un script exact des étapes — ne pas improviser
- Parle pendant que tu manipules l'app
- Si quelque chose ne marche pas, garde ton calme : *"Je vais vous montrer ça différemment…"* et passe à la vidéo de backup
- Durée idéale : 3-5 minutes

---

### Slide 17 — Ce que j'ai appris

**Titre** : `Bilan — Ce que j'ai appris`

**Contenu suggéré** (personnel, sincère) :
- Traitement du signal audio (DSP) de zéro
- Programmation multithreading et synchronisation
- Conception d'UI custom (Knob, Fader, VU-mètre)
- Architecture logicielle propre (interfaces, séparation couches)
- Ce que je ferais différemment : [ta réflexion honnête]

**Conseil** : Sois authentique. Le jury/public apprécie l'honnêteté sur les limites autant que les succès. *"J'aurais mieux organisé X dès le début"* montre de la maturité.

---

### Slide 18 — Questions ?

**Titre** : `Merci — Des questions ?`

**Contenu** :
- Lien GitHub (si public)
- Ton nom + contact
- Éventuellement : *"L'app tourne — venez essayer !"*

**Visuel** : Même style que la slide titre, sobre et lisible.

---

## Schémas récapitulatifs — Instructions détaillées

### Schéma A — Flux audio global (Slide 6)

**Formes** : 4 rectangles à coins arrondis + 3 flèches épaisses
**Texte** :
- Rect 1 : "Entrée" / "Micro / Guitare" (icône 🎙️)
- Rect 2 : "Moteur Audio" / "AudioEngine.cs" (icône ⚙️)
- Rect 3 : "Chaîne d'effets" / "FX1 → FX2 → FX3" (icône 🎸)
- Rect 4 : "Sortie" / "Haut-parleurs" (icône 🔊)
**Couleurs** : Fond des rectangles en bleu foncé (#1a2a4a), texte blanc, flèches orange (#ff6b00)

---

### Schéma B — Pipeline audio (Slide 7)

**Formes** : 1 grand rectangle en pointillés (thread audio) + rectangle central + flèches circulaires
**Texte dans le rectangle central** :
1. "Lire 256 samples du micro"
2. "Appliquer les effets"
3. "Retourner 256 samples traités"
**Couleurs** : Grand rectangle fond bleu nuit, petit rectangle fond blanc/clair, flèche circulaire orange

---

### Schéma C — Chaîne de pédales (Slide 8)

**Formes** : 6-10 rectangles en ligne (ou 2 lignes), reliés par des flèches
**Chaque rectangle** : Nom de l'effet + icône emoji en dessous
**Couleurs** : Alterner entre 2-3 couleurs pour les pédales (ex. orange, bleu, vert)
**Note** : Ajouter une note "Drag & drop pour réorganiser" avec une petite flèche courbe

---

### Schéma D — Soft vs Hard Clipping (Slide 9)

**Formes** : 2 graphiques avec axes X/Y tracés à la main
**Dans chaque graphique** :
- Ligne de seuil en rouge (horizontale)
- Courbe de signal en bleu/blanc
- Zone "clippée" en rouge/orange
**Différence visuelle** : Soft = courbe lisse et arrondie au seuil / Hard = plateau plat et abrupt au seuil

---

### Schéma E — Flux Save/Load presets (Slide 13)

**Formes** : 3 boîtes reliées par des flèches, répétées 2 fois (save + load)
**Icônes** : 💾 pour "Réglages actuels", 📄 pour "Fichier JSON", ⚙️ pour "Moteur audio"
**Flèches Save** : direction droite, couleur bleue
**Flèches Load** : direction droite, couleur verte

---

## Questions fréquentes du public — Prépare tes réponses

1. *"Quelle est la latence ?"* → Environ 5-15 ms avec WASAPI, < 5 ms avec ASIO — imperceptible pour la plupart des usages.
2. *"C'est multiplateforme ?"* → Grâce à Avalonia, oui : Windows, Mac, Linux. NAudio est Windows-first mais des alternatives existent.
3. *"Comment tu as appris le traitement audio ?"* → Documentation en ligne, algorithmes classiques (Freeverb open-source), essais/erreurs.
4. *"Peut-on ajouter d'autres effets ?"* → Oui, grâce à l'interface `IAudioEffect` — il suffit d'implémenter 2 méthodes.
5. *"Pourquoi pas utiliser un DAW existant ?"* → L'objectif était pédagogique : comprendre les mécanismes de l'intérieur.
6. *"Le code est ouvert ?"* → Oui (si tu as mis le repo en public) — montrer le lien GitHub.
7. *"C'est quoi un buffer ?"* → Un petit paquet de son (256 nombres) — traiter par paquets est plus efficace que nombre par nombre.

---

*Guide de présentation du projet AudioBlocks — CFC Informatique*
