using Microsoft.AspNetCore.Mvc.Rendering;

namespace PAYLO_WEB.Helpers
{
    public static class Utilities
    {
        public static string ActiveClass(this IHtmlHelper htmlHelper, string controllers = null
            , string actions = null, string cssClass = "active", int selectedValue = 0, int initValue = 0)
        {
            var currentController = htmlHelper?.ViewContext.RouteData.Values["controller"] as string;
            var currentAction = htmlHelper?.ViewContext.RouteData.Values["action"] as string;

            var acceptedControllers = (controllers ?? currentController ?? "").Split(',');
            var acceptedActions = (actions ?? currentAction ?? "").Split(',');

            return acceptedControllers.Contains(currentController) && acceptedActions.Contains(currentAction)
                ? cssClass
                : "";
        }
    }
}
