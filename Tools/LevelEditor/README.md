# Bullet Foundry Level Editor

Open `index.html` in a browser. No server or installation is required.

1. Edit level settings, card restrictions, and enemy spawns.
2. Export the JSON file.
3. Put it under `Assets/Levels` in Unity.
4. Select a level button in `LevelSelect.unity` and drag the JSON into its **Level Json** field.

For a runtime external file, set **External Json Location** on the button and enable **Prefer External Json**. Relative paths check `persistentDataPath` first and then `StreamingAssets`; HTTP(S) URLs are also supported.
