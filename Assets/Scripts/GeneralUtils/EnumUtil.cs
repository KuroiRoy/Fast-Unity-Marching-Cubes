﻿using System;

namespace SkywardRay.Utility {
    public static class EnumUtil<TEnum> where TEnum : Enum {

        public static readonly TEnum[] allValues;
        public static readonly int[] intValues;
        public static readonly (int index, TEnum value)[] valuePairs;
        public static readonly int length;

        static EnumUtil () {
            allValues = (TEnum[])Enum.GetValues(typeof(TEnum));
            length = allValues.Length;
            
            intValues = new int[length];
            valuePairs = new (int, TEnum)[length];

            for (var i = 0; i < length; i++) {
                intValues[i] = (int)(object)allValues[i];
                valuePairs[i] = (intValues[i], allValues[i]);
            }
        }

    }
}
