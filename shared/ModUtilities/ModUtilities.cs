using System;

namespace IMModUtilities
{
    public class ModUtilities
    {
        public static void ProbeGameObject(GameObject gameObject)
        {
            Debug.Log("Probing Object: " + gameObject.name);

            Component[] comps = gameObject.GetComponentsInChildren<Component>();

            string compName;
            string prefix;
            string suffix;
            foreach (Component comp in comps)
            {
                prefix = "";
                suffix = "";
                compName = comp.GetType().Name;

                if (comp is RectTransform rt)
                {
                    suffix = " (" + rt.rect.width + " x " + rt.rect.height + ")";
                }
                if (comp is CanvasGroup cg)
                {
                    suffix = " (alpha: " + cg.alpha + ")";
                }
                if (comp is TextMeshProUGUI tmp)
                {
                    suffix = " (" + tmp.text + ")";
                }
                if (comp is Text tx)
                {
                    suffix = " (" + tx.text + ")";
                }
                if (comp is Lang_Button lb)
                {
                    suffix = " (" + lb.Constant + ")";
                }
                if (comp is Image im)
                {
                    Sprite sprite = im.sprite;
                    suffix = " (";
                    if (sprite != null)
                    {
                        suffix += "sprite: " + sprite.name + ", ";
                    }
                    suffix += im.color.ToString();
                    suffix += ")";
                }
                if (comp is RawImage ri)
                {
                    Texture texture = ri.texture;
                    if (texture != null)
                    {
                        suffix = " (texture: " + texture.name + ")";
                    }
                }
                if (comp is Button btn)
                {
                    int eventCount = btn.onClick.GetPersistentEventCount();
                    if (eventCount > 0)
                    {
                        suffix = " (" + btn.onClick.GetPersistentTarget(0).GetType().Name + "." + btn.onClick.GetPersistentMethodName(0);

                        int i = 1;
                        while (i < eventCount)
                        {
                            suffix += ", " + btn.onClick.GetPersistentTarget(i).GetType().Name + "." + btn.onClick.GetPersistentMethodName(i);
                            i++;
                        }
                        suffix += ")";
                    }
                }

                Transform trans = comp.gameObject.transform;
                while (trans != null)
                {
                    prefix = trans.gameObject.name + ":" + prefix;
                    trans = trans.parent;
                }
                Debug.Log(prefix + compName + suffix);

            }
        }
    }

}