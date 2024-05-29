using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
This file is a port of texture_packer by Coeuvre Wong. Original license text:

The MIT License (MIT)

Copyright (c) 2014 Coeuvre Wong

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace GITweaks
{
    public class GITweaksTexturePacker
    {
        // Skyline
        struct Skyline
        {
            public int x;
            public int y;
            public int w;

            public int left => x;
            public int right => x + w - 1;
        }

        private RectInt border;
        private List<Skyline> skylines;
        private int padding;
        private int extrusion;

        public GITweaksTexturePacker(int maxWidth, int maxHeight, int padding, int extrusion)
        {
            border = new RectInt(0, 0, maxWidth, maxHeight);
            skylines = new List<Skyline>()
            {
                new Skyline
                {
                    x = 0,
                    y = 0,
                    w = maxWidth
                }
            };
            this.padding = padding;
            this.extrusion = extrusion;
        }

        private bool CanPut(int i, int w, int h, out RectInt rect)
        {
            rect = new RectInt(skylines[i].x, 0, w, h);
            int widthLeft = rect.width;
            while (true)
            {
                if (i >= skylines.Count)
                    return false;

                rect.y = Mathf.Max(rect.y, skylines[i].y);
                if (!border.Contains(rect))
                {
                    return false;
                }
                if (skylines[i].w >= widthLeft)
                {
                    return true;
                }
                widthLeft -= skylines[i].w;
                i++;
            }
        }

        private bool FindSkyline(int w, int h, out int resultIndex, out RectInt resultRect)
        {
            int bottom = int.MaxValue;
            int width = int.MaxValue;
            int index = -1;
            RectInt rect = new RectInt(0, 0, 0, 0);

            // keep the `bottom` and `width` as small as possible
            for (int i = 0; i < skylines.Count; i++)
            {
                if (CanPut(i, w, h, out var r))
                {
                    if (r.Bottom() < bottom || (r.Bottom() == bottom && skylines[i].w < width))
                    {
                        bottom = r.Bottom();
                        width = skylines[i].w;
                        index = i;
                        rect = r;
                    }
                }
            }

            resultIndex = index;
            resultRect = rect;
            return index >= 0;
        }

        private void Split(int index, RectInt rect)
        {
            var skyline = new Skyline
            {
                x = rect.x,
                y = rect.Bottom() + 1,
                w = rect.width,
            };

            skylines.Insert(index, skyline);

            int i = index + 1;
            while (i < skylines.Count)
            {
                if (skylines[i].left <= skylines[i - 1].right)
                {
                    int shrink = skylines[i - 1].right - skylines[i].left + 1;
                    if (skylines[i].w <= shrink)
                    {
                        skylines.RemoveAt(i);
                    }
                    else
                    {
                        var s = skylines[i];
                        s.x += shrink;
                        s.w -= shrink;
                        skylines[i] = s;
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void Merge()
        {
            int i = 1;
            while (i < skylines.Count)
            {
                if (skylines[i - 1].y == skylines[i].y)
                {
                    var s = skylines[i - 1];
                    s.w += skylines[i].w;
                    skylines[i - 1] = s;

                    skylines.RemoveAt(i);
                    i -= 1;
                }
                i += 1;
            }
        }

        public bool Pack(int width, int height, out RectInt frame)
        {
            width += padding + extrusion * 2;
            height += padding + extrusion * 2;

            if (FindSkyline(width, height, out int i, out RectInt rect))
            {
                Split(i, rect);
                Merge();

                rect.width -= padding + extrusion * 2;
                rect.height -= padding + extrusion * 2;

                frame = rect;
                return true;
            }

            frame = default;
            return false;
        }

        public bool CanPack(int width, int height)
        {
            if (FindSkyline(width + padding + extrusion * 2, height + padding + extrusion * 2, out _, out var rect))
            {
                Skyline skyline = new Skyline
                {
                    x = rect.x,
                    y = rect.yMax + 1,
                    w = rect.width,
                };

                return skyline.right <= border.yMax && skyline.y <= border.yMin;
            }
            return false;
        }
    }
}