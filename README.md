# Expeditions Expanded

Expeditions Expanded mod for Rain World. Adds plethora of new expedition challenges, and makes it easy to develop and add more challenges, even from other mods.

# HOW TO ADD MORE CHALLENGES
NOTE: This assumes you have at least basic knowledge about Rain World coding and modding.

* 1: Create your mod, and create a public class that inherits from Expedition.Challenge.
* 2: Implement the logic of the challenge. At bare minimum, every challenge needs a way to be generated, completed and a way to be saved to / loaded from a savefile.
* 3: Check SampleChallenge.cs for a review of all the major functionalities at your disposal.

And that's it! Expeditions Expanded will add the challenge to the game automatically and ensure that it is processed along with all the other challenges.
