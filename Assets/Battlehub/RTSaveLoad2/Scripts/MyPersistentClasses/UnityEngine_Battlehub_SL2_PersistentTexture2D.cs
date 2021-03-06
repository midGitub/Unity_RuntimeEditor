#if !RTSL2_MAINTENANCE
using Battlehub.RTSaveLoad2;
using Battlehub.RTCommon;
using ProtoBuf;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Battlehub.SL2
{
    [CustomImplementation]
    public partial class PersistentTexture2D
    {        

        [ProtoMember(1)]
        private byte[] m_bytes;

        [ProtoMember(2)]
        private bool m_isReadable;

        public override void ReadFrom(object obj)
        {
            base.ReadFrom(obj);

            Texture2D texture = (Texture2D)obj;
            if (texture == null)
            {
                return;
            }

            try
            {
                m_bytes = texture.EncodeToPNG();
                m_isReadable = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.ToString());
                m_bytes = new byte[0];
                m_isReadable = false;
            }
        }

        public override object WriteTo(object obj)
        {
            obj = base.WriteTo(obj);
            Texture2D texture = (Texture2D)obj;
            if (texture == null)
            {
                return null;
            }

            if (m_isReadable)
            {
                texture.LoadImage(m_bytes, false);
            }
            return texture;

        }

        public override void GetDeps(GetDepsContext context)
        {
            base.GetDeps(context);
        }

        public override void GetDepsFrom(object obj, GetDepsFromContext context)
        {
            base.GetDepsFrom(obj, context);
        }
    }
}
#endif

