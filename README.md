# Warudo OVRLipSync Animation Node

This project provides a Warudo Node for generating OVR Lip Sync blendshape animation data using the OVRLipSync.dll provided by Oculus.

## Installation

1. Download the OVRLipSync.dll file from [Oculus Lip Sync Unity Integration](https://developer.oculus.com/downloads/package/oculus-lipsync-unity/) (You will need to extract the dll from the unitypackage, it will be in the `Assets/Oculus/LipSync/Plugins/Win64/` folder).

2. Place the `OVRLipSync.cs` and `OVRLipSyncNode.cs` files into your `Steam/steamapps/common/Warudo/Warudo_Data/StreamingAssets/Playground` folder.

3. Create a folder named `MonoBleedingEdge` within `Steam/steamapps/common/Warudo/Warudo_Data/`.

4. Place the downloaded `OVRLipSync.dll` file into the `MonoBleedingEdge` folder.

## Usage

To utilize the `Generate OVR Lip Sync Animations` node in your blueprint, follow these steps:

1. Replace the existing `Generate Lip Sync Animations` node with the `Generate OVR Lip Sync Animations` node in your blueprint.

2. From the provided dropdown menu, select your character.

3. Press the `Auto Map Visemes` button within the node. This action will prompt the node to attempt mapping the correct blendshapes to the visemes automatically.

4. Verify that the visemes are mapped correctly to your model.

5. Choose the microphone you wish to use for generating the viseme data.

## Support

For any inquiries or troubleshooting assistance, feel free to reach out to me.

- Email: support@ximmer.dev
- Discord: @Ximmer
- Twitch: https://www.twitch.tv/ximmer_vr
- Bluesky: https://bsky.app/profile/ximmer.dev
- Carrd: https://ximmer.carrd.co/

## License

- This project is licensed under the [MIT License](LICENSE).

## Acknowledgements

- This project utilizes the `OVRLipSync.dll` and `OVRLipSync.cs` file provided by Oculus under the [Oculus Audio SDK License Version 3.3](https://developer.oculus.com/licenses/audio-3.3/).
