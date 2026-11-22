using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace NeonBreach
{
    /// <summary>
    /// World-space menu panel for VR (the desktop IMGUI menus are not visible inside a headset).
    /// Point with the right controller (or right hand) - a laser shows the pointer - and click with the trigger,
    /// the A button or a pinch. The panel is placed 1.6 m in front of the player when a page opens.
    /// Content is supplied every frame by VRMenuDriver.
    /// </summary>
    public class VRMenu : MonoBehaviour
    {
        public struct Item
        {
            public string label;
            public System.Action action;
            public Item(string l, System.Action a) { label = l; action = a; }
        }

        class Row
        {
            public GameObject go;
            public TextMesh text;
            public Renderer bg;
        }

        const int MaxRows = 11;
        const float RowH = 0.115f;

        VRRig rig;
        GameObject panel;
        TextMesh title, body;
        Renderer panelBg;
        readonly List<Row> rows = new List<Row>();
        List<Item> items = new List<Item>();
        LineRenderer pointer;
        Transform cursor;
        Material rowMat, rowHoverMat, panelMat;
        string page = "";
        int hover = -1;
        bool prevClick;
        bool visible;

        public bool Visible { get { return visible; } }

        public void Init(VRRig r)
        {
            rig = r;
            panel = new GameObject("VRMenuPanel");
            panelMat = MakeMat(new Color(0.01f, 0.03f, 0.07f, 0.88f));
            rowMat = MakeMat(new Color(0.03f, 0.1f, 0.16f, 0.95f));
            rowHoverMat = MakeMat(new Color(0.1f, 0.55f, 0.65f, 0.95f));

            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.name = "Background";
            bg.transform.SetParent(panel.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            bg.transform.localScale = new Vector3(1.5f, 1.9f, 1f);
            panelBg = bg.GetComponent<Renderer>();
            panelBg.sharedMaterial = panelMat;

            title = rig.MakeText(panel.transform, "Title", 72, 0.012f, TextAnchor.UpperCenter);
            title.alignment = TextAlignment.Center;
            title.transform.localPosition = new Vector3(0f, 0.9f, -0.01f);
            body = rig.MakeText(panel.transform, "Body", 56, 0.0062f, TextAnchor.UpperCenter);
            body.alignment = TextAlignment.Center;
            body.color = new Color(0.85f, 0.95f, 1f, 1f);
            body.transform.localPosition = new Vector3(0f, 0.74f, -0.01f);

            for (int i = 0; i < MaxRows; i++)
            {
                Row row = new Row();
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Row" + i;
                q.layer = Layers.UI;
                q.transform.SetParent(panel.transform, false);
                q.transform.localScale = new Vector3(1.25f, RowH - 0.015f, 1f);
                Object.Destroy(q.GetComponent<Collider>());
                BoxCollider bc = q.AddComponent<BoxCollider>();
                bc.size = new Vector3(1f, 1f, 0.02f);
                row.bg = q.GetComponent<Renderer>();
                row.bg.sharedMaterial = rowMat;
                row.text = rig.MakeText(panel.transform, "RowText" + i, 56, 0.0075f, TextAnchor.MiddleCenter);
                row.text.alignment = TextAlignment.Center;
                row.text.color = Color.white;
                row.go = q;
                rows.Add(row);
            }
            Util.NoShadows(panel);

            GameObject pg = new GameObject("MenuPointer");
            pointer = pg.AddComponent<LineRenderer>();
            pointer.positionCount = 2;
            pointer.useWorldSpace = true;
            pointer.startWidth = 0.005f;
            pointer.endWidth = 0.003f;
            pointer.sharedMaterial = Mats.FxTracer;
            pointer.startColor = new Color(0.2f, 0.95f, 1f, 0.9f);
            pointer.endColor = new Color(0.2f, 0.95f, 1f, 0.3f);
            pointer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject c = Util.Part(pg.transform, PrimitiveType.Sphere, "Cursor", Vector3.zero, Vector3.zero, Vector3.one * 0.02f, "NeonCyan");
            cursor = c.transform;
            cursor.SetParent(null, true);
            Util.NoShadows(c);

            panel.SetActive(false);
            pointer.gameObject.SetActive(false);
            cursor.gameObject.SetActive(false);
        }

        static Material MakeMat(Color c)
        {
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.color = c;
            return m;
        }

        /// <summary>Show (or keep showing) a page. A different pageId re-places the panel in front of the player.</summary>
        public void Show(string pageId, string titleText, string bodyText, List<Item> list)
        {
            if (panel == null) return;
            if (!visible || pageId != page)
            {
                page = pageId;
                Place();
                hover = -1;
                prevClick = true;   // ignore the click that opened this page
            }
            visible = true;
            if (!panel.activeSelf) panel.SetActive(true);
            items = list;

            SetText(title, titleText);
            SetText(body, bodyText);
            int lines = string.IsNullOrEmpty(bodyText) ? 0 : bodyText.Split('\n').Length;
            float top = 0.74f - lines * 0.062f - (lines > 0 ? 0.05f : 0f);
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                bool on = i < items.Count;
                if (r.go.activeSelf != on) { r.go.SetActive(on); r.text.gameObject.SetActive(on); }
                if (!on) continue;
                float y = top - RowH * 0.5f - i * RowH;
                r.go.transform.localPosition = new Vector3(0f, y, -0.005f);
                r.text.transform.localPosition = new Vector3(0f, y, -0.012f);
                SetText(r.text, items[i].label);
                r.bg.sharedMaterial = i == hover ? rowHoverMat : rowMat;
            }
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;
            page = "";
            if (panel != null) panel.SetActive(false);
            if (pointer != null) pointer.gameObject.SetActive(false);
            if (cursor != null) cursor.gameObject.SetActive(false);
        }

        static void SetText(TextMesh t, string s)
        {
            if (s == null) s = "";
            if (t.text != s) t.text = s;
        }

        void Place()
        {
            Camera c = Camera.main;
            if (rig == null) return;
            Transform head = c != null ? c.transform : rig.transform;
            Vector3 fwd = rig.HeadForwardFlat;
            Vector3 pos = head.position + fwd * 1.6f;
            pos.y = head.position.y - 0.1f;
            panel.transform.position = pos;
            panel.transform.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
            Vector3 e = panel.transform.eulerAngles;
            panel.transform.rotation = Quaternion.Euler(0f, e.y, 0f);
        }

        void LateUpdate()
        {
            if (!visible || rig == null || rig.RightHand == null)
            {
                if (pointer != null && pointer.gameObject.activeSelf) pointer.gameObject.SetActive(false);
                if (cursor != null && cursor.gameObject.activeSelf) cursor.gameObject.SetActive(false);
                return;
            }

            Transform hand = rig.RightHand;
            Vector3 origin = hand.position;
            Vector3 dir = hand.forward;
            Vector3 end = origin + dir * 3f;
            hover = -1;
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, 6f, 1 << Layers.UI, QueryTriggerInteraction.Collide))
            {
                end = hit.point;
                for (int i = 0; i < rows.Count && i < items.Count; i++)
                    if (rows[i].go == hit.collider.gameObject) { hover = i; break; }
            }
            if (!pointer.gameObject.activeSelf) pointer.gameObject.SetActive(true);
            pointer.SetPosition(0, origin);
            pointer.SetPosition(1, end);
            cursor.gameObject.SetActive(hover >= 0);
            cursor.position = end;
            for (int i = 0; i < rows.Count && i < items.Count; i++)
                rows[i].bg.sharedMaterial = i == hover ? rowHoverMat : rowMat;

            // click: right trigger / A button / right-hand pinch (or left trigger if there is no right controller)
            InputDevice R = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            InputDevice L = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool click = Pressed(R, CommonUsages.triggerButton) || Pressed(R, CommonUsages.primaryButton) ||
                         (!R.isValid && Pressed(L, CommonUsages.triggerButton)) ||
                         (rig.Hands != null && rig.Hands.Right.pinch);
            if (click && !prevClick && hover >= 0 && hover < items.Count)
            {
                Sfx.Play2D(SfxId.Click, 0.8f);
                Haptics.PulseXR(XRNode.RightHand, 0.3f, 0.03f);
                System.Action a = items[hover].action;
                if (a != null) a();
            }
            prevClick = click;
        }

        static bool Pressed(InputDevice d, InputFeatureUsage<bool> u)
        {
            bool v;
            return d.isValid && d.TryGetFeatureValue(u, out v) && v;
        }
    }
}
