using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace UltraSaveSystem
{
    public class UnityContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            
            if (property.PropertyType == typeof(Vector3) && 
                (property.PropertyName == "normalized" || property.PropertyName == "magnitude" || property.PropertyName == "sqrMagnitude"))
            {
                property.ShouldSerialize = instance => false;
            }
            
            if (property.PropertyType == typeof(Quaternion) && property.PropertyName == "normalized")
            {
                property.ShouldSerialize = instance => false;
            }
            
            return property;
        }
    }
}