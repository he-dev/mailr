using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Mailr.Extensions.Utilities.Mvc.ModelBinding
{
    public class EmailViewBinder : IModelBinder
    {
        // https://docs.microsoft.com/en-us/aspnet/core/mvc/advanced/custom-model-binding?view=aspnetcore-2.2

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext.ValueProvider.GetValue(bindingContext.ModelName) is var value && value != ValueProviderResult.None)
            {
                if (Enum.TryParse<EmailView>(value.FirstValue, ignoreCase: true, out var view))
                {
                    bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
                    bindingContext.Result = ModelBindingResult.Success(view);
                }
                else
                {
                    bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid view value.");
                    bindingContext.Result = ModelBindingResult.Failed();
                }
            }
            else
            {
                // this is the default
                bindingContext.Result = ModelBindingResult.Success(EmailView.Original);
            }

            return Task.CompletedTask;
        }
    }
}
