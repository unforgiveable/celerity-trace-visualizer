# Celerity Trace Visualizer

## General Information

This tool was developed as part of my master thesis titled "Visualizing Celerity Traces in VR".
Its purpose is to load, proccess and visualize execution traces produced by the Celerity project ([homepage](https://celerity.github.io/), [github](https://github.com/celerity/celerity-runtime)).
The final thesis is provided as part of this repository and serves to document the internal design of the application.

**Abstract:**
`The increasing availability of Virtual Reality (VR) hardware in the consumer space opens the door for novel
application areas to take advantage of its unique properties. Its ability to simulate virtual three-dimensional
space presents an opportunity for the field of data virtualization. Especially in cases where the data is too
complex to effectively visualize on traditional monitors this added dimension has the potential to dramatically
increase visual clarity. One source of such complex data is the Celerity project. Its goal is to
automatically distribute a given program across a cluster of distributed memory compute nodes. Due to
the challenges presented by this task, its runtime behavior tends to be quite complex in nature. In order to
analyze its performance an execution trace can be recorded during its runtime, allowing the user to inspect its
inner workings after the fact. In this thesis a VR application for visualizing these Celerity execution traces is
implemented, showcasing the potential advantages the hardware platform offers when compared to traditional
solutions.`

## Installation

### Requirements

- Unity 2021.3.27f1 LTS or later version of 2021 LTS
- Any OpenXR compatible VR runtime for VR usage (only tested with SteamVR)

### Setup Instructions

- Clone this repo and add the folder ```Trace-Visualizer``` as a project in Unity Hub (```Open > Add project from disk```)
- Open the project from the list with the correct Unity Editor version and wait for it to retrieve the required packages
- Open The ```MainScene``` scene by navigating to the folder ```Assets > Scenes``` in the Editor and double clicking on ```MainScene```. You should see a grid world with a round platform in the middle.

#### Input Profiles for different VR Hardware

The project is only tested with the Valve Index headset and Knuckles controllers.
If you want to add an input profile for different (OpenXR-supported) VR Hardware do the following steps:

- Go to ```Edit > Project Settings > XR Plug-in Management > OpenXR```
- Under ```OpenXR Feature Groups```:
  - In ```Interaction Profiles``` add the profile corresponding to your hardware. The input methods in the application should be mapped as closely as possible to your controllers
  - If your preferred runtime is not selected automatically by the OXR driver select it in the ```Play Mode OpenXR Runtime``` dropdown

#### Disabling VR Mode for Testing

- Go to ```Edit > Project Settings > XR Plug-in Management > OpenXR```
- Under ```OpenXR Feature Groups > All Features``` enable ```Mock Runtime``` if you want to run in non-VR mode, disable for VR mode
- Disable to re-enable the VR mode

### Running the Application

- (for VR mode:) Start your VR runtime (e.g. SteamVR) and make sure your headset and controllers are fully connected and ready to go
- Press the play button at the top of the editor to start the application
- (for VR mode:) Put on your headset
