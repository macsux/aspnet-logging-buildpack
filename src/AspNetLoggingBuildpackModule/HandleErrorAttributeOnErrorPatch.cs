using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using HarmonyLib;

namespace AspNetLoggingBuildpackModule
{
    /// <summary>
    /// Reflectively patch MVC error handling mechanism so we're not tied to specific MVC version
    /// </summary>
    [HarmonyPatch]
    public class HandleErrorAttributeOnErrorPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var handleErrorAttributeType = Type.GetType("System.Web.Mvc.HandleErrorAttribute, System.Web.Mvc");
            if (handleErrorAttributeType != null)
            {            
                yield return AccessTools.Method(handleErrorAttributeType, "OnException");
            }
        }
        // static void Prefix(object __instance, object __0, ref bool __state)
        // {
        //     if(__state)
        //         return; // some weirdness going on with this class method as it's double firing. this prevents it
        //     __state = true;
        //     dynamic _this = __instance;
        //     if (__0 == null || __0.GetType().Name != "ExceptionContext")
        //         return;
        //     dynamic filterContext = __0;
        //     if (filterContext.IsChildAction || filterContext.ExceptionHandled)
        //         return;
        //     
        //     Exception exception = filterContext.Exception;
        //     if (new HttpException((string) null, exception).GetHttpCode() != 500 || !_this.ExceptionType.IsInstanceOfType((object) exception))
        //         return;
        //     Console.Error.WriteLine(exception);
        // }

        static void Postfix(object __instance, object __0)
        {
            if (__0 == null || __0.GetType().Name != "ExceptionContext")
                return;
            dynamic filterContext = __0;
            if (filterContext.IsChildAction || !filterContext.HttpContext.IsCustomErrorEnabled)
                return;
            Exception exception = filterContext.Exception;
            Console.Error.WriteLine(exception);

        }
    }
}