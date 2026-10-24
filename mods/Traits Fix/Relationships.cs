using HarmonyLib;
using static TraitFix.TraitsFix;

namespace TraitFix
{
    [HarmonyPatch(typeof(Relationships._relationship), nameof(Relationships._relationship.Initialize))]
    public class Relationships__relationship_Initialize
    {
        public static void Postfix(Relationships._relationship __instance)
        {
            if (!TryGetRelationshipGirls(__instance, out data_girls.girls girl0, out data_girls.girls girl1))
                return;

            int age0 = girl0.GetAge();
            int age1 = girl1.GetAge();
            if (girl0.trait == traits._trait._type.Maternal && age0 > age1)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            else if (girl1.trait == traits._trait._type.Maternal && age1 > age0)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            else if (girl0.trait == traits._trait._type.Precocious && age0 < age1)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            else if (girl1.trait == traits._trait._type.Precocious && age1 < age0)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
        }
    }

    [HarmonyPatch(typeof(Relationships), nameof(Relationships.Do_Dynamic))]
    public class Relationships_Do_Dynamic
    {
        public static void Postfix()
        {
            if (Relationships.RelationshipsData == null)
                return;

            // Every pair asks whether its idols are center; the singles are looked up once for all of them
            TraitLookups lookups = new TraitLookups();
            foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
            {
                if (!TryGetRelationshipGirls(relationship, out data_girls.girls girl0, out data_girls.girls girl1))
                    continue;

                if (relationship.Dynamic == Relationships._relationship._dynamic.positive)
                    AdjustForAgeTraits(relationship, girl0, girl1);

                if (girl0.trait == traits._trait._type.Arrogant && IsCenter(girl0, lookups))
                    relationship.Add(ARROGANT_WEEKLY_PENALTY);
                else if (girl1.trait == traits._trait._type.Arrogant && IsCenter(girl1, lookups))
                    relationship.Add(ARROGANT_WEEKLY_PENALTY);
            }
        }

        private static void AdjustForAgeTraits(Relationships._relationship relationship, data_girls.girls girl0, data_girls.girls girl1)
        {
            if (girl0.trait == traits._trait._type.Maternal && girl0.GetAge() > girl1.GetAge())
                relationship.Add(MATERNAL_WEEKLY_BONUS);
            else if (girl1.trait == traits._trait._type.Maternal && girl1.GetAge() > girl0.GetAge())
                relationship.Add(MATERNAL_WEEKLY_BONUS);

            if (girl0.trait == traits._trait._type.Precocious && girl0.GetAge() < girl1.GetAge())
                relationship.Add(PRECOCIOUS_WEEKLY_BONUS);
            else if (girl1.trait == traits._trait._type.Precocious && girl1.GetAge() < girl0.GetAge())
                relationship.Add(PRECOCIOUS_WEEKLY_BONUS);
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), nameof(Relationships._relationship.Recalc))]
    public class Relationships__relationship_Recalc
    {
        public static void Postfix(Relationships._relationship __instance)
        {
            if (!TryGetRelationshipGirls(__instance, out data_girls.girls girl0, out data_girls.girls girl1)
                || __instance.Ratio >= FORGIVING_THR)
                return;

            if (girl0.trait == traits._trait._type.Forgiving || girl1.trait == traits._trait._type.Forgiving)
                __instance.Ratio = FORGIVING_THR;
        }
    }
}
