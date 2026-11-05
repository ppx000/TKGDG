using System;
using System.Collections.Generic;

namespace FlowerEscape.EditorTools
{
    // Mirrors Assets/FlowerEscape/Layout/gameplay_layout.json, exported from the Figma file.
    // Coordinates are Figma points inside the 402x874 frame, origin top-left, y down.
    [Serializable]
    public class FigmaLayoutSpec
    {
        public string fileKey;
        public float frameWidth;
        public float frameHeight;
        public float exportScale;
        public List<FigmaLayoutNode> nodes;
    }

    [Serializable]
    public class FigmaLayoutNode
    {
        public string id;
        public string name;
        public string state;     // Combat | BuyGear (shared layers are exported once, under Combat)
        public string group;     // UI group the layer belongs to (Battlefield, Board, HUD_TopLeft, ...)
        public string parent;    // id of the layer this one is nested under, or empty
        public string kind;      // image | text
        public string sprite;    // file name in Art/Figma, without extension
        public string uiName;    // GameObject name for interactive layers (Btn_Start, ...)
        public bool button;
        public bool stretchX;    // stretches with the screen width (9-sliced)
        public float sliceX;     // left/right 9-slice border in Figma points

        public float x, y, w, h;      // drawn rect (render bounds for images)
        public float bx, by, bw, bh;  // Figma bounding box

        public string text;
        public float fontSize;
        public string color;
        public string strokeColor;
        public float strokeWidth;
        public string alignH;
        public string alignV;
    }
}
