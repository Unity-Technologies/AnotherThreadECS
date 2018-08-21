using UnityEngine;

namespace UTJ {

public static class TouchUtil
{
    public enum Info
    {
        None = 99,
        Began = 0,
        Moved = 1,
        Stationary = 2,
        Ended = 3,
        Canceled = 4,
    }

    private static Vector2 prev_position_ = Vector2.zero;

    public static Info GetInfo()
    {
        if (Input.touchCount > 0) {
            return (Info)((int)Input.GetTouch(0).phase);
        } else {
            if (Input.GetMouseButtonDown(0)) { return Info.Began; }
            if (Input.GetMouseButton(0)) { return Info.Moved; }
            if (Input.GetMouseButtonUp(0)) { return Info.Ended; }
        }
        return Info.None;
    }

    public static Vector2 GetPosition()
    {
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);
            Vector2 pos = Vector3.zero;
            pos.x = touch.position.x;
            pos.y = touch.position.y;
            return pos;
        } else {
            Info touch = GetInfo();
            if (touch != Info.None) {
                return Input.mousePosition;
            }
        }
        return Vector2.zero;
    }

    public static Vector2 GetMotionRaw()
    {
        Info touch = GetInfo();
        switch (touch) {
            case Info.None:
                return Vector2.zero;
            case Info.Began:
                prev_position_ = GetPosition();
                return Vector2.zero;
            case Info.Moved:
                var pos = GetPosition();
                var diff = pos - prev_position_;
                prev_position_ = pos;
                return diff;
        }
        return Vector2.zero;
    }
    public static Vector2 GetMotion()
    {
        var vec = GetMotionRaw();
        vec.x /= Screen.width;
        vec.y /= Screen.height;
        return vec;
    }
}

} // namespace UTJ {
