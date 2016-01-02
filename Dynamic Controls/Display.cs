﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dynamic_Controls
{
    class Display
    {
        static Texture2D displayTex;
        static Color32[] background;
        public Display(int width, int height)
        {
            if (displayTex == null)
                displayTex = new Texture2D(width, height);
            else
                displayTex.Resize(width, height);
            background = new Color32[width * height];
            for (int i = 0; i < background.Length; i++)
                background[i] = XKCDColors.Black;
            displayTex.SetPixels32(background);
            displayTex.Apply();
        }

        public void Clear()
        {
            displayTex.SetPixels32(background);
        }

        public void drawPoints(List<List<float>> listToDraw, int x_max, int y_max, bool lastIsGreen)
        {
            Clear();
            for (int i = 0; i < listToDraw.Count; i++)
            {
                List<float> l = listToDraw[i];
                int xLoc = (int)Mathf.Clamp(l[0] * displayTex.width / x_max, 2, displayTex.width - 3);
                int yLoc = (int)Mathf.Clamp(l[1] * displayTex.height / y_max, 2, displayTex.height - 3);

                for (int j = -2; j <= 2; j++)
                {
                    for (int k = -2; k <= 2; k++)
                    {
                        if (lastIsGreen && i == listToDraw.Count - 1)
                            displayTex.SetPixel(xLoc + j, yLoc + k, XKCDColors.Green);
                        else
                            displayTex.SetPixel(xLoc + j, yLoc + k, XKCDColors.Red);
                    }
                }
            }
            displayTex.Apply();
        }

        public Texture2D Image
        {
            get
            {
                return displayTex;
            }
        }
    }
}