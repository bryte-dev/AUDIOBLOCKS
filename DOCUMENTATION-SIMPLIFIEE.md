# AudioBlocks — Documentation Simplifiée

> Version vulgarisée destinée aux développeurs qui ne connaissent pas l'audio.
> Tout est expliqué avec des analogies du quotidien, sans formules mathématiques.

---

## Table des matières

1. [AudioBlocks, c'est quoi ?](#1-audioblocks-cest-quoi-)
2. [Comment ça marche ?](#2-comment-ça-marche-)
3. [Les technologies utilisées](#3-les-technologies-utilisées)
4. [Le moteur audio](#4-le-moteur-audio)
5. [Les effets audio](#5-les-effets-audio)
6. [Les presets](#6-les-presets)
7. [L'enregistreur](#7-lenregistreur)
8. [Le métronome](#8-le-métronome)
9. [L'interface utilisateur](#9-linterface-utilisateur)
10. [Mini-glossaire](#10-mini-glossaire)

---

## 1. AudioBlocks, c'est quoi ?

AudioBlocks est une application qui **modifie le son en temps réel**. Imagine un guitariste sur scène avec une rangée de petites boîtes posées au sol — on appelle ça des pédales d'effets. Quand il appuie dessus avec le pied, le son de sa guitare change : il devient plus grave, plus craquant, plus écho… AudioBlocks fait exactement la même chose, mais en logiciel, sur ton ordinateur.

Concrètement : tu branches ton micro (ou ta guitare via une interface audio), tu choisis quels effets tu veux appliquer, et le son modifié sort dans tes haut-parleurs — tout ça sans délai perceptible. Tu peux aussi enregistrer le résultat et le sauvegarder en fichier audio.

C'est ce qu'on appelle un **pedalboard virtuel** : toutes les pédales d'effets d'un musicien professionnel, remplacées par un logiciel gratuit qui tourne sur n'importe quel PC.

---

## 2. Comment ça marche ?

Le parcours du son est simple et linéaire, comme une chaîne de montage :

```
┌─────────────┐     ┌─────────────┐     ┌──────────────────────┐     ┌─────────────┐
│   Micro /   │────▶│   Moteur    │────▶│   Chaîne d'effets    │────▶│   Haut-     │
│   Guitare   │     │   audio     │     │  Effet 1 → Effet 2   │     │   parleurs  │
│  (entrée)   │     │             │     │  → Effet 3 → ...     │     │  (sortie)   │
└─────────────┘     └─────────────┘     └──────────────────────┘     └─────────────┘
```

1. **Entrée** : Le son arrive depuis ton micro ou ta carte son.
2. **Moteur audio** : Il "découpe" le son en toutes petites tranches (des milliers par seconde) et les envoie aux effets.
3. **Chaîne d'effets** : Chaque effet reçoit la tranche, la modifie, et la passe au suivant — comme des cuisiniers qui se passent un plat à améliorer.
4. **Sortie** : Le son modifié repart vers les haut-parleurs.

Tout ça se passe environ **100 fois par seconde**, si vite que ton oreille n'entend aucun décalage.

---

## 3. Les technologies utilisées

| Technologie | Rôle simple |
|---|---|
| **C# / .NET 8** | Le langage de programmation — c'est avec ça qu'est écrit tout le code |
| **Avalonia UI** | La bibliothèque qui dessine les fenêtres et les boutons (fonctionne sur Windows, Mac, Linux) |
| **NAudio** | La bibliothèque qui parle à la carte son — lit et écrit le son au bon format |
| **WASAPI / ASIO** | Des "protocoles" qui permettent de communiquer avec la carte son le plus vite possible |
| **JSON** | Le format de fichier utilisé pour sauvegarder les presets (réglages) |

---

## 4. Le moteur audio

Imagine un **chef cuisinier** dans une grande cuisine industrielle. Les clients (les haut-parleurs) crient des commandes toutes les quelques millisecondes : *"Donne-moi les 256 prochains morceaux de son !"*

Le chef (le moteur audio) doit **impérativement** répondre dans les temps — sinon on entend des craquements ou des coupures. C'est pourquoi tout le traitement audio se fait dans un **thread dédié** (un cuisinier assigné uniquement à cette tâche, qui ne fait rien d'autre).

Quand le chef reçoit une commande :
1. Il récupère les 256 nouveaux morceaux de son brut depuis le micro.
2. Il les passe en cuisine à la chaîne d'effets.
3. Il retourne les 256 morceaux transformés aux haut-parleurs.

> **Pour les curieux** : En termes techniques, le son numérique est une suite de nombres décimaux entre -1.0 et +1.0, chacun représentant la position du haut-parleur à un instant précis. On appelle chaque nombre un *sample*. Le moteur traite 256 samples à la fois (un *buffer*), environ 187 fois par seconde à 48 000 Hz.

---

## 5. Les effets audio

Chaque effet est une petite boîte qui reçoit des nombres (le son), les modifie selon sa logique, et renvoie des nombres modifiés. Les effets sont branchés en série : la sortie de l'un est l'entrée du suivant.

---

### 🔊 Gain — Le bouton de volume

**Analogie** : C'est simplement un bouton de volume. Tu montes le gain, le son est plus fort. Tu le baisses, le son est plus discret.

**Comment ça marche** : Chaque nombre du son est multiplié par un coefficient. ×2 = deux fois plus fort. ×0.5 = deux fois moins fort. C'est vraiment aussi simple que ça.

---

### 🚪 Noise Gate — La porte automatique

**Analogie** : Imagine une porte automatique dans un supermarché. Quand quelqu'un passe devant le capteur (le son est fort), la porte s'ouvre. Quand il n'y a personne (silence ou bruit de fond très faible), la porte se ferme.

**Comment ça marche** : L'effet surveille le volume du son. En dessous d'un seuil (le "threshold"), le son est coupé complètement. Au-dessus, il passe normalement. Ça évite qu'on entende le souffle du micro ou le bourdonnement de la pièce quand le musicien ne joue pas.

---

### 🤜 Compresseur — L'équilibreur de volume

**Analogie** : Imagine quelqu'un assis à côté de toi pendant que tu regardes un film, avec une télécommande. Quand une explosion retentit (son très fort), il baisse le volume. Quand les personnages parlent doucement, il le remonte. Le compresseur fait ça automatiquement, des milliers de fois par seconde.

**Comment ça marche** : L'effet surveille en permanence le niveau du son. Dès qu'il dépasse un certain seuil (trop fort), il réduit automatiquement le volume d'un certain facteur (le "ratio"). Résultat : les sons forts deviennent moins forts, les sons faibles restent tels quels → la dynamique est réduite, tout est plus équilibré.

---

### 🔥 Distortion — L'overdrive chaud

**Analogie** : Imagine que tu essaies de verser trop d'eau dans un verre déjà presque plein. L'eau déborde un peu, mais doucement, en suivant la courbe du verre. La distortion, c'est pareil : elle "arrondit" les pics du son qui dépassent une limite.

**Comment ça marche** : Quand le son est trop fort (un nombre proche de +1 ou -1), l'effet l'arrondit en douceur au lieu de le laisser dépasser. Techniquement, c'est une fonction mathématique qui "aplatit" les sommets de la courbe sonore (on appelle ça du *soft clipping*). Résultat : un son chaud, légèrement craquant — typique des amplificateurs à lampes.

---

### ⚡ Fuzz — La distorsion agressive

**Analogie** : Même idée que la distortion, mais cette fois tu écrases le verre avec ton poing. Pas d'arrondi progressif — le son est violemment coupé net dès qu'il dépasse la limite.

**Comment ça marche** : Le son est d'abord amplifié très fort, puis "coupé" brutalement au-delà d'un seuil (on appelle ça du *hard clipping*). Tout ce qui dépasse est aplati à plat, comme si tu coupais avec des ciseaux. Le résultat est un son agressif, crunchy — typique des guitares rock/metal des années 60-70.

---

### 🎚️ EQ (Égaliseur 3 bandes) — La table de mixage simplifiée

**Analogie** : Sur n'importe quelle chaîne hi-fi, tu as trois boutons : Basses, Médiums, Aigus. Tu montes les basses pour plus de boom, tu baisses les aigus si c'est trop strident. L'EQ fait exactement la même chose.

**Comment ça marche** : Le son est divisé en trois groupes de fréquences (graves = sons lents/profonds, médiums = voix/instruments, aigus = sons rapides/brillants). Chaque groupe peut être amplifié ou atténué indépendamment. Techniquement, ce sont des "filtres IIR" — des formules mathématiques qui laissent passer certaines fréquences et en bloquent d'autres.

---

### 🎛️ Graphic EQ — La version pro à 10 curseurs

**Analogie** : Même chose que l'EQ simple, mais au lieu de 3 boutons, tu en as 10 — chacun contrôle une plage de fréquences plus précise (comme sur les anciennes chaînes hi-fi haut de gamme ou dans les studios).

**Comment ça marche** : 10 filtres indépendants, chacun centré sur une fréquence précise (31 Hz, 62 Hz, 125 Hz, 250 Hz, 500 Hz, 1 kHz, 2 kHz, 4 kHz, 8 kHz, 16 kHz). Tu peux booster ou couper chacun indépendamment. Donne un contrôle très fin sur la "couleur" du son.

---

### 🏔️ Delay — L'écho en montagne

**Analogie** : Tu cries "Allo !" dans une montagne et tu entends "Allo... allo... allo..." quelques secondes après, de plus en plus faible. C'est exactement ce que fait le delay.

**Comment ça marche** : L'effet garde en mémoire une copie du son passé (une sorte de "bande magnétique" virtuelle). Il la rejoue après un certain délai, avec un volume légèrement réduit. Si on réinjecte cette copie dans l'entrée du delay, on obtient plusieurs répétitions de plus en plus faibles — exactement comme un écho.

---

### ⛪ Reverb — Le son dans une cathédrale

**Analogie** : Chante dans une salle de bain carrelée. Le son rebondit sur tous les murs en même temps et crée une longue traîne — ce sentiment de "spaciosité". La reverb reproduit artificiellement cet effet pour n'importe quel son.

**Comment ça marche** : L'effet utilise plusieurs dizaines de "mini-delays" simultanés, chacun avec un délai et une durée différents, pour simuler les rebonds d'un son dans une pièce. AudioBlocks utilise l'algorithme **Freeverb** — une méthode classique inventée dans les années 90 et encore utilisée aujourd'hui. Le paramètre "Room Size" contrôle la taille de la salle simulée.

---

### 🎤 Chorus — La chorale virtuelle

**Analogie** : Quand une chorale chante la même note, personne ne chante exactement au même moment ni exactement à la même hauteur. Ces micro-décalages créent un son plus riche et plus large. Le chorus reproduit ça avec un seul instrument.

**Comment ça marche** : L'effet duplique le son une ou plusieurs fois, puis fait légèrement varier la vitesse de lecture de chaque copie (avec un LFO — un oscillateur très lent). Ces micro-variations de vitesse changent imperceptiblement la hauteur de chaque copie, créant cet effet de "chœur".

---

## 6. Les presets

Un preset, c'est une **sauvegarde complète de tous tes réglages**. Tu as trouvé une combinaison d'effets qui sonne bien ? Tu la sauvegardes sous un nom ("Son rock classique", "Voix de robot"), et tu peux la recharger en un clic plus tard.

Techniquement, les presets sont sauvegardés dans des fichiers **JSON** — un format texte simple que tu peux même ouvrir dans un éditeur de texte. Chaque preset contient la liste des effets actifs et tous leurs paramètres.

> **Pour les curieux** : Un fichier preset ressemble à `{ "effects": [{ "name": "Distortion", "drive": 0.7, "mix": 0.8 }, ...] }`. C'est lisible par un humain et facile à partager.

---

## 7. L'enregistreur

L'enregistreur te permet de **capturer le son traité** (après tous les effets) et de l'**exporter en fichier WAV**.

Le fonctionnement est simple : pendant que le moteur audio tourne, l'enregistreur copie discrètement chaque tranche de son dans un grand tableau en mémoire. Quand tu arrêtes l'enregistrement, il prend tout ce tableau et l'écrit dans un fichier WAV standard — lisible par n'importe quel lecteur audio.

> **Note technique** : L'enregistrement se fait sur le thread audio (très rapide) mais l'écriture du fichier se fait sur le thread principal (interface). Un mécanisme de verrouillage (`lock`) empêche les deux threads de modifier le tableau en même temps, ce qui évite des corruptions de données.

---

## 8. Le métronome

Le métronome produit un "tic-tac" régulier pour aider un musicien à jouer en rythme. Tu lui donnes un tempo en BPM (battements par minute) et il génère un click sonore à intervalles parfaitement réguliers.

La particularité d'AudioBlocks : le click est **synthétisé en temps réel** (calculé mathématiquement, comme une note de musique) plutôt que lu depuis un fichier audio. Avantage : aucun délai de chargement, et la précision du timing est quasi-parfaite même sur de longues durées.

---

## 9. L'interface utilisateur

L'interface d'AudioBlocks ressemble à un vrai panneau d'effets audio, avec des contrôles inspirés du matériel physique :

- **Knob (potentiomètre rotatif)** : Un bouton rond qu'on tourne, comme sur un ampli ou un synthétiseur. Tu cliques et tu fais glisser la souris vers le haut pour augmenter la valeur.
- **Fader (curseur linéaire)** : Une réglette qui monte et descend, comme sur une table de mixage. Idéal pour le volume.
- **VU-mètre** : Une colonne de petits carrés colorés (vert = normal, orange = fort, rouge = trop fort) qui montre le niveau du son en temps réel. C'est ce qu'on voit sur les enregistreurs et les tables de mixage.
- **Graphic EQ Control** : Une rangée de 10 curseurs pour l'égaliseur graphique.

---

## 10. Mini-glossaire

| Terme | Ce que ça veut dire simplement |
|---|---|
| **Sample** | Un seul "point" de mesure du son à un instant précis — comme un pixel pour une image |
| **Buffer** | Un petit paquet de samples traités ensemble (environ 256 à la fois) |
| **Sample rate** | Le nombre de samples par seconde — 48 000 Hz = 48 000 mesures par seconde |
| **Latence** | Le délai entre jouer une note et l'entendre dans les haut-parleurs |
| **dB (décibel)** | L'unité du volume sonore — +6 dB = deux fois plus fort |
| **Fréquence (Hz)** | La vitesse de vibration du son — graves = peu de Hz, aigus = beaucoup de Hz |
| **Dry/Wet** | Dry = son original, Wet = son modifié par l'effet — le mix contrôle le dosage |
| **Thread** | Un fil d'exécution parallèle dans le programme — le thread audio s'occupe uniquement du son |
| **Clipping** | Quand le son dépasse la limite (±1) et crée des craquements non voulus |
| **Preset** | Une sauvegarde complète de tous les réglages d'effets, rechargeable en un clic |

---

*Documentation simplifiée du projet AudioBlocks — rédigée pour des développeurs non-initiés à l'audio.*
