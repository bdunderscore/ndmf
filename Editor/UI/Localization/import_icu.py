import icu
import json

locales = icu.Locale.getAvailableLocales()

print("# Derived from ICU dataset. See import_icu.py")

displayNames = {}
for locale in locales:
    icu_locale = icu.Locale(locale)
    print(f"{locale}={icu_locale.getDisplayName(icu_locale)}")
