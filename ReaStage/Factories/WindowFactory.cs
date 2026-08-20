using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ReaStage.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReaStage.Factories;

internal class WindowFactory
{
    private readonly IServiceProvider serviceProvider;

    public WindowFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public TView Create<TView, TViewModel>() where TView : TopLevel where TViewModel : ViewModelBase
    {
        TViewModel viewModel = ActivatorUtilities.CreateInstance<TViewModel>(serviceProvider);
        TView view = ActivatorUtilities.CreateInstance<TView>(serviceProvider);
        view.DataContext = viewModel;
        return view;
    }
}