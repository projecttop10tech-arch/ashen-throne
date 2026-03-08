# Ashen Throne — Launch Checklist

## Pre-Launch Code Verification

- [x] Zero compilation errors in Unity Editor
- [x] All 6 scenes load without NullReferenceException
- [x] 44 test files covering all systems
- [x] Integration tests: CombatFlow, EconomyFlow, BootSequence, Performance benchmarks
- [x] P2W enforcement verified: IAPManager rejects combat power, BattlePass blocks P2W rewards, gacha has zero heroes
- [x] GDPR consent flow (PrivacyConsentManager)
- [x] Deep link handling (ashenthrone:// scheme)
- [x] Scene transitions (SceneTransitionOverlay)
- [x] Settings persistence (SettingsManager via PlayerPrefs)
- [x] Haptic feedback (HapticFeedbackManager, respects accessibility)
- [x] UI animation system (UIAnimationHelper, respects ReduceMotion)
- [x] Colorblind filter shader (3 Daltonization modes)
- [x] 8-language localization (219 keys per language)
- [x] NotificationScheduler wired to all major events
- [x] Battle Pass Season 1 (50 tiers, zero P2W)
- [x] Gacha pool (40 cosmetics, zero heroes)
- [x] 201 ScriptableObject assets generated
- [x] 132 placeholder images, 51 prefabs, 18 audio files

## External Dependencies (Requires Manual Setup)

- [ ] Install PlayFab Unity SDK, set Title ID, uncomment real auth code
- [ ] Install Photon Fusion 2 SDK, set App ID
- [ ] Install Firebase SDK (Analytics + Crashlytics), set GoogleService-Info.plist / google-services.json
- [ ] Install Unity IAP, configure products in App Store Connect + Google Play Console
- [ ] Deploy backend/CloudScript/economy.js to PlayFab
- [ ] Replace placeholder art with production assets (Phases 11-12)
- [ ] Replace placeholder audio with production tracks (Phase 12)
- [ ] Set up real signing keys (iOS provisioning profile, Android keystore)
- [ ] Configure CI/CD with real credentials
- [ ] Set up TestFlight and Google Play Internal Testing tracks

## Store Submission Checklist

### iOS (App Store Connect)
- [ ] Real Team ID in ExportOptions.plist
- [ ] Info.plist: NSUserTrackingUsageDescription (ATT)
- [ ] App icon: 1024x1024
- [ ] Screenshots: 5 per device class (iPhone 6.7", 6.5", 5.5"; iPad 12.9")
- [ ] Privacy nutrition labels configured
- [ ] Age rating: IARC PEGI 12 / ESRB T
- [ ] Localized descriptions in 8 languages

### Android (Google Play Console)
- [ ] Target API 34+
- [ ] Production keystore signing
- [ ] App icon: 512x512
- [ ] Feature graphic: 1024x500
- [ ] Screenshots: 5+ per device class
- [ ] Privacy policy URL
- [ ] Content rating questionnaire
- [ ] Localized store listings in 8 languages

## Monitoring Plan

### Key Metrics (Daily)
| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Crash-free rate | >99.5% | <99.0% |
| D1 retention | >40% | <30% |
| D7 retention | >20% | <15% |
| Avg session length | 15-25 min | <10 min |
| Tutorial completion | >80% | <60% |
| IAP conversion | >2% | <1% |

### Dashboards
1. Firebase Crashlytics: Real-time crash monitoring
2. Firebase Analytics: User flows, session data, conversion funnels
3. PlayFab: Player data, economy health, server-side metrics
4. Custom balance dashboard: Resource generation vs spending rates

### On-Call Rotation
- Week 1-2 post-launch: Daily monitoring, 4h response SLA for critical bugs
- Week 3-4: Bi-daily monitoring, 8h response SLA
- Ongoing: Weekly balance reviews, monthly content updates
