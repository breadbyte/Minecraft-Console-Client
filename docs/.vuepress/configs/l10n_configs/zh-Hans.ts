/* This file is automatically generated by "gen_configs.py" */
import type { SiteLocaleData  } from '@vuepress/shared'
import type { DefaultThemeLocaleData } from '@vuepress/theme-default'
import { headConfig } from '../head.js'

const Translation = require('../../translations/zh-Hans.json')

export const mainConfig_zh_Hans: SiteLocaleData  = {
    lang: 'zh-Hans',
    title: Translation.title,
    description: Translation.description,
    head: headConfig
}

export const defaultThemeConfig_zh_Hans: DefaultThemeLocaleData = {
    selectLanguageName: "简体中文",
    selectLanguageText: Translation.theme.selectLanguageText,
    selectLanguageAriaLabel: Translation.theme.selectLanguageAriaLabel,

    navbar: [
        {
            text: Translation.navbar.AboutAndFeatures,
            link: "/l10n/zh-Hans/guide/",
        },
        
        {
            text: Translation.navbar.Installation,
            link: "/l10n/zh-Hans/guide/installation.md",
        },
      
        {
            text: Translation.navbar.Usage,
            link: "/l10n/zh-Hans/guide/usage.md",
        },
      
        {
            text: Translation.navbar.Configuration,
            link: "/l10n/zh-Hans/guide/configuration.md",
        },
      
        {
            text: Translation.navbar.ChatBots,
            link: "/l10n/zh-Hans/guide/chat-bots.md",
        },
    ],

    sidebar: [
        "/l10n/zh-Hans/guide/README.md", 
        "/l10n/zh-Hans/guide/installation.md", 
        "/l10n/zh-Hans/guide/usage.md", 
        "/l10n/zh-Hans/guide/configuration.md", 
        "/l10n/zh-Hans/guide/chat-bots.md", 
        "/l10n/zh-Hans/guide/creating-bots.md", 
        "/l10n/zh-Hans/guide/contibuting.md"
    ],

    // page meta
    editLinkText: Translation.theme.editLinkText,
    lastUpdatedText: Translation.theme.lastUpdatedText,
    contributorsText: Translation.theme.contributorsText,

    // custom containers
    tip: Translation.theme.tip,
    warning: Translation.theme.warning,
    danger: Translation.theme.danger,

    // 404 page
    notFound: Translation.theme.notFound,
    backToHome: Translation.theme.backToHome,

    // a11y
    openInNewWindow: Translation.theme.openInNewWindow,
    toggleColorMode: Translation.theme.toggleColorMode,
    toggleSidebar: Translation.theme.toggleSidebar,
}