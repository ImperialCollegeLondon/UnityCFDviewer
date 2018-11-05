https://github.com/ImperialCollegeLondon/UnityCFDviewer

This project provides a reference/demo/proof-of-concept implementation of some of the things we have been talking about. It's not necessarily intended as a starting point for a full implementation, but it could function as one if needbe. It doesn't integrate the already-written audio feedback system, but that would be easy to do.

Some notes on the implementation, and thoughts on future directions:. 

Controls

Input is VR only, i.e. the only facility for moving the camera or 'pointer' at present is via Oculus Touch controllers. A real implementation probably needs a keyboard/mouse fall-back solution. Controls and notes:
* Movement: Hold the left hand inside thumb button (side of controller) and move the left hand about. I've played with several movement solutions over the last 12 months or so, and I think this one is the best for general-purpose viewers like this. After a minute or so of practice it is pretty intuitive, and is MUCH nicer than 'teleportation'.
* Viewer scaling: Click the two buttons on top of the left controller to zoom in and out. There are three scale levels, and the viewer 'tweens' between these on a click. View position when zooming in is the tip of the pointer. [This is possibly bugged at the moment]. Again, I've played with different approaches here... scaling is something you have to be careful with for nausea reasons. This system is a little bit clunky, but should provide all the control we need, and it's also fairly intuitive after a little practice. Scaling may not even be needed in this application, but let's provide it as an option anyway. Note this implementation scales the viewer (VR camera rig) not the model. This can cause hassles with scaling any hand-held UI (see below), so for this application we might be better off scaling the model instead.
* Rotation. There are (deliberately) no view rotation controls - the viewer rotates by just turning the head. This is 100% intuitive and easy, but can make is hard to look backwards (cables get wrapped round, controllers end up in non-optimal positions for the sensors). View rotation with controls (one of the joysticks?) is easy enough to implement, but I've found that smooth rotation induces instant nausea. Rotation can be stepped (e.g. one click of the joystick left flicks you 45 degrees left), but this breaks the feeling of VR immersion.
* Pointer. The right hand is linked to a 'pointer', a wand-like object with a clear 'tip' - the tip size indicates scalars (pressure in this example). This paradigm provides a way to interact with particular points in the model, but it also works as a 'UI' pointer. There is no UI implemented in this model project, but see below. At present this pointer has only one function - to indicate velocity. Clicking the right hand index finger trigger button launches a 'particle' from the tip, and holding down the inside button (side of the controller) releases a flame-like stream of short lived particle emitters to provide a complementary visualisation.

Why is there a room and a table?
Why not! Room is just for fun and may not be a good idea in the long run. Table may be a useful paradigm, as we can put controls (i.e. a gui) on the table next to the CFD grid.


What it does:
- Holds a large 4D array of 3D vectors to represent a velocity field of several timeslices. Currently this is set to a 33 x 33 x 3 grid, with 2 timeslices, with data read from a hard-coded position. XML reader is NOT a multipurpose reader at all, but should read multiple scalars and vectors if they are in the expected format
- Provides marker objects to show the edge of this grid in 3D space
- Provides arrow objects spaced evenly throughout the grid, scaled with the magnitude of the velocity, and pointing in the correct direction. These could also be coloured according to velocity, though that's not implemented yet. Arrows can be switched on/off via a public bool in the main script attached to 'CFDobject'
- Smoothly implements time-flow at a rate controlled by 'time factor' in the main script. Arrows interpolate scale and position as time advances. At present when it reaches the end of the simulation, it resets to the start.
- Allows the user to release a 'particle' into the grid to track flow. These particles move by reading velocity vectors direct from the data grid at the current timepoint, (linearly) interpolating between datapoints. They move once per frame (90 frames/second on Rift), so increments are small and hopefully artefacts from discretized movement will be unproblematic. Particles have a comet-like trail rendered using the Unity particle system. Additionally, the user can release continuous streams of short-lived particles from their pointer, as an alternative visualisation
- Visualises pressure by scaling the tip of the 'wand' object

Implementation:
Most of the code is in CFDObjectScript.cs, attached to a single root-level gameobject 'CFDObject', which also acts as the parent for all procedurally instantiated objects (arrows, grid boundary markers, particles). There is also code attached to the Particle Prefab (ParticleScript.cs) to handle particle lifecycle and iteration, and an input manager 'TouchControlScript.cs' attached to the OVRCameraRig object - this reads controllers and handles movement and scaling. 'statics.cs' implements a bodge I always use in Unity to provide scripts with a reference to singleton scripts, in this case to the main CFDObjectScript.

UI
We will need one. The user will need at least
- Playback of CFD timeslices (speed, pause button, position slider, play button)
- Visualisation setup (e.g. turn velocity arrow grid on/off, choose which parameters are visualised using sound, size of pointer tip, colour, etc tc)
- An 'exit' button
- Ideally a file browser to load a CFD file for visualisation - though this could be done outside VR if needbe

From experience with playing with different UIs, and from other software, a good UI paradigm is a hand-held 3D console, attached to the left hand position. The console can have an integrated screen which provides text feedback (e.g. mouseover tooltips, readouts of parameters at the pointer tip, etc). Icons and controls are placed on the console, either as traditional 2D icons or (better) as 3D models, and clicked/dragged with the pointer held in the right hand. Whether 2D or 3D their interaction/animation/feedback needs to be manually programmed - I'm not aware of any library or framework that does this neatly, certainly not for 3D models. Unity has a built in UI framework that can be persuaded to work on a canvas mapped into 3D space - but it's not very easy to use, and will not naturally work with a 3D pointer.  I've written a system that does this for another project, and if we want to go this way I can integrate it into this one. It's not exactly a proper framework though, and is quite deeply embedded into the other project's code - if we want to use it I can hack it out and rejig it into something more generalised, but that's not an especially quick job.

When I say 'left hand' and 'right hand' we would need to be able to swap them over for left handed operators - but this is easy enough, it just involves reparenting the objects.

One caveat with this though - it assumes the operator is using both touch controllers. If the right hand is holding a haptic feedback device it might not work so well... though IIRC this does let you move a 3D cursor around, so perhaps it will be OK. If the right hand can't be used to point then a UI becomes much harder to implement, and I'm not sure what it should look like - perhaps the console paradigm would still work but be statically positioned relative to the camera, and operated with the left hand not the right?


