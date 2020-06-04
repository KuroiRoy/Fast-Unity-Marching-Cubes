using System;
using Den.Tools.GUI;
using UnityEditor;
using UnityEngine;

namespace UnityTemplateProjects.TerrainGeneration.Editor.Utils {

public class EditorGrid {
    
    private static Material gridMaterial;
    private static readonly int shaderPropertyColor = Shader.PropertyToID("_Color");
    private static readonly int shaderPropertyBackground = Shader.PropertyToID("_Background");
    private static readonly int shaderPropertyCellSizeX = Shader.PropertyToID("_CellSizeX");
    private static readonly int shaderPropertyCellSizeY = Shader.PropertyToID("_CellSizeY");
    private static readonly int shaderPropertyCellOffsetX = Shader.PropertyToID("_CellOffsetX");
    private static readonly int shaderPropertyCellOffsetY = Shader.PropertyToID("_CellOffsetY");
    private static readonly int shaderPropertyLineOpacity = Shader.PropertyToID("_LineOpacity");
    private static readonly int shaderPropertyBordersOpacity = Shader.PropertyToID("_BordersOpacity");
    private static readonly int shaderPropertyDisplayRect = Shader.PropertyToID("_Rect");

    public static void StaticGrid (Rect displayRect, float cellSize, Color color, Color background = new Color(), bool fadeWithZoom = true) {
        if (UI.current.layout) return;

        var dispCellSize = new Vector2(cellSize, cellSize);
        dispCellSize *= UI.current.scrollZoom.zoom;

        var dispOffset = new Vector2(-UI.current.scrollZoom.scroll.x + displayRect.x,
            UI.current.scrollZoom.scroll.y - displayRect.height - displayRect.y); //-1 makes the line pass through 0-1 pixel
        dispOffset.x += dispCellSize.x * 10000;
        dispOffset.y += dispCellSize.y * 10000; //hiding the line pass through 0-1 pixel in 10000 cells away

        DrawGrid(displayRect, dispCellSize, dispOffset, color, background, 1, 0, fadeWithZoom);
    }

    private static void DrawGrid (Rect displayRect, Vector2 cellSize, Vector2 cellOffset, Color color, Color background, float lineOpacity, float bordersOpacity, bool fadeWithZoom) {
        if (UI.current.layout) return;

        if (Math.Abs(background.a + background.r + background.g + background.b) < float.Epsilon)
            background = new Color(color.r, color.g, color.b, 0); //to avoid blacking on fadeOnZoom

        if (gridMaterial == null) gridMaterial = new Material(Shader.Find("Hidden/TerrainGeneration/EditorGrid"));

        if (fadeWithZoom) {
            var clampZoom = UI.current.scrollZoom != null ? UI.current.scrollZoom.zoom : 1;
            if (clampZoom > 1) clampZoom = 1;
            color = color * clampZoom + background * (1 - clampZoom);
        }

        var dpiFactor = UI.current.DpiScaleFactor;

        gridMaterial.SetColor(shaderPropertyColor, color);
        gridMaterial.SetColor(shaderPropertyBackground, background);

        gridMaterial.SetFloat(shaderPropertyCellSizeX, cellSize.x * dpiFactor);
        gridMaterial.SetFloat(shaderPropertyCellSizeY, cellSize.y * dpiFactor);
        gridMaterial.SetFloat(shaderPropertyCellOffsetX, cellOffset.x * dpiFactor);
        gridMaterial.SetFloat(shaderPropertyCellOffsetY, cellOffset.y * dpiFactor);

        gridMaterial.SetFloat(shaderPropertyLineOpacity, lineOpacity);
        gridMaterial.SetFloat(shaderPropertyBordersOpacity, bordersOpacity);

        gridMaterial.SetVector(shaderPropertyDisplayRect, new Vector4(displayRect.x, displayRect.y, displayRect.size.x, displayRect.size.y) * dpiFactor);

        EditorGUI.DrawPreviewTexture(displayRect, StylesCache.blankTex, gridMaterial, ScaleMode.StretchToFill);
    }

}

}