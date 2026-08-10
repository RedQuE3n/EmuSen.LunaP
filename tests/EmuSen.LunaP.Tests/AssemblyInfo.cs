using Xunit;

// This suite does not run in parallel, and the reason is structural rather than
// a workaround. Every test here dispatches onto the ONE headless Avalonia
// session UiTest owns, and several fixtures configure statics that are global
// to that application - LunaSettings.Store, LunaSettings.Diagnostics, and the
// applied theme's resource dictionary. Running test classes concurrently means
// one class's constructor replacing another class's diagnostics hook while that
// class is mid-assertion.
//
// It was found by CI rather than locally: A_malformed_css_theme_leaves_the_
// previous_one_in_force passed on a developer machine and failed on a two-core
// runner, because ThemeTests' constructor had taken the Diagnostics hook away
// from CssThemeTests between the write and the assert. Parallelism was buying
// nothing here anyway - the dispatcher is a single thread and the whole suite
// is four seconds - so the honest fix is to stop claiming it. See docs/LunaP.md §20.2.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
