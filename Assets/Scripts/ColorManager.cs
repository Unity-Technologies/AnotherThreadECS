using UnityEngine;

public static class Color
{
    public enum Type {
        // when you change here, shader code is also needed to be modified. look for "_color_table".
        Black,
        Red,
        Blue,
        Magenta,
        Green,
        Yellow,
        Cyan,
        White,
        Max,
    }
    
    public static Vector4[] color_shared_data_;

    public static void Initialize()
    {
        color_shared_data_ = new Vector4[(int)Type.Max];
        color_shared_data_[(int)Type.Black] = new Vector4(0f, 0f, 0f, 1f);
        color_shared_data_[(int)Type.Red] = new Vector4(1f, 0f, 0f, 1f);
        color_shared_data_[(int)Type.Blue] = new Vector4(0f, 0f, 1f, 1f);
        color_shared_data_[(int)Type.Magenta] = new Vector4(1f, 0f, 1f, 1f);
        color_shared_data_[(int)Type.Green] = new Vector4(0f, 1f, 0f, 1f);
        color_shared_data_[(int)Type.Yellow] = new Vector4(1f, 1f, 0f, 1f);
        color_shared_data_[(int)Type.Cyan] = new Vector4(0f, 1f, 1f, 1f);
        color_shared_data_[(int)Type.White] = new Vector4(1f, 1f, 1f, 1f);
    }

    public static void Setup(Material material)
    {
        material.SetVectorArray("_color_table", color_shared_data_);
    }
}
