# Unity Marching Cubes Implementation
This is a fork of https://github.com/Fobri/Fast-Unity-Marching-Cubes.


## About the project
I was originally checking out the code in from Fobri because the mesh updates after modifying the terrain was so fast. Afterwards I tried implementing some ways I had played with before to speed up terrain generation. Like, expanding out from finished chunks by checking which sides contain the surface. Thus keeping the amount of chunks needed down.

## Problems
After my changes it's not yet possible to modify the terrain. And there are some artifacts on chunk edges that I need to look into.

## Controls
WASD to move, Space and X for up and down, right click to control the camera, F to fill and C to cut terrain.

## Known problems
If you build the project the build might randomly crash. Doesn't happen in editor, no clue why.

## Collaboration
Feel free to fork the project and submit a pull request if you manage to optimize it even further :)
Parts of the code are taken from https://github.com/Eldemarkki/Marching-Cubes-Terrain (those parts are mentioned in the comments)

Recommended Unity version 2019.3.14f1


Contact me

Discord: KuroiRoy#0956

Or contact Fobri through his original repository
