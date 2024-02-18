import icu
import json

locales = icu.Locale.getAvailableLocales()

displayNames = {}
for locale in locales:
    icu_locale = icu.Locale(locale)
    displayNames[locale] = icu_locale.getDisplayName(icu_locale)

displayNames["__comment__"] = "Derived from ICU dataset. See import_icu.py";

print(json.dumps(displayNames, indent=4, ensure_ascii=False))