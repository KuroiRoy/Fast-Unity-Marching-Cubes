# Unity Marching Cubes Implementation
This is a fork of https://github.com/Fobri/Fast-Unity-Marching-Cubes.
I have been working on it a lot in the past few weeks so most things have changed.

## About the project
I was originally checking out the code in from Fobri because the mesh updates after modifying the terrain was so fast. Afterwards I tried implementing some ways I had played with before to speed up terrain generation. Like, expanding out from finished chunks by checking which sides contain the surface. Thus keeping the amount of chunks needed down.

## Problems
- There are some artifacts in the generated chunk data that show as a line of small blobs in the mesh.
- There is some noticable stutter when a lot of chunks are updated. I haven't found a way to handle this since you can't change the order of the scheduled jobs. I would like to handle the most important chunks first and spread the rest out over multiple frames.

## Controls
WASD to move, Space and X for up and down, right click to control the camera, F to fill and C to cut terrain.

## Known problems
Maybe I haven't been playing around with it enough but I didn't notice any issues currently.

## Collaboration
Feel free to fork the project and submit a pull request if you manage to optimize it even further :)
Parts of the code are taken from https://github.com/Eldemarkki/Marching-Cubes-Terrain (those parts are mentioned in the comments)

The original recommended Unity version was 2019.3.14f1. But i've been working in 2020.2.0a13


Contact me

Discord: KuroiRoy#0956

Or contact Fobri through his original repository
