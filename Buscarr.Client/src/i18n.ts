const ptBR = {
  api: {
    loadUserError: (status: number) => `Erro ${status} ao carregar o usuário.`,
    wrongCredentials: 'E-mail ou senha incorretos.',
    loginError: (status: number) => `Erro ${status} ao entrar.`,
    emailTaken: 'Já existe um usuário com esse e-mail.',
    invalidRegistration: 'Preencha todos os campos. A senha precisa ter pelo menos 8 caracteres.',
    registerError: (status: number) => `Erro ${status} ao criar o usuário.`,
    logoutError: (status: number) => `Erro ${status} ao sair.`,
    searchError: (status: number) => `Erro ${status} na busca.`,
    artistError: (status: number) => `Erro ${status} ao carregar o artista.`,
    releasesError: (status: number) => `Erro ${status} ao carregar os releases.`,
    settingsError: (status: number) => `Erro ${status} ao carregar as configurações.`,
    saveSettingsError: (status: number) => `Erro ${status} ao salvar as configurações.`,
  },
  login: {
    email: 'E-mail',
    name: 'Nome',
    password: 'Senha',
    signIn: 'Entrar',
    createUser: 'Criar usuário',
    haveUser: 'Já tenho usuário',
    tagline: 'Encontre o artista certo.',
  },
  search: {
    greeting: (name: string) => `Olá, ${name}`,
    logout: 'Sair',
    settings: 'Configurações',
    artistName: 'Nome do artista',
    search: 'Buscar',
    searching: 'Buscando…',
    notFound: 'Nenhum artista encontrado.',
    error: 'Erro na busca.',
    hint: 'Busque pelo nome do artista.',
  },
  releases: {
    back: 'Voltar',
    albums: 'Álbuns',
    eps: 'EPs',
    singles: 'Singles',
    all: 'Todos',
    loading: 'Carregando…',
    empty: 'Nenhum release oficial encontrado.',
    studio: 'Estúdio',
    live: 'Ao vivo',
    other: 'Outros',
    tracks: (count: number) => (count === 1 ? '1 faixa' : `${count} faixas`),
    type: 'Tipo de release',
  },
  settings: {
    title: 'Configurações',
    edition: 'Quando um álbum tiver várias edições, baixar:',
    original: 'A original',
    originalHint: 'As faixas do lançamento original.',
    remastered: 'A remasterizada',
    remasteredHint: 'A remasterização mais recente.',
    deluxe: 'A deluxe',
    deluxeHint: 'A edição deluxe, com faixas extras.',
    fallback: 'Se o álbum não tiver essa edição, fica a original.',
    loading: 'Carregando…',
    saved: 'Salvo.',
  },
};

type Messages = typeof ptBR;

const en: Messages = {
  api: {
    loadUserError: (status) => `Error ${status} loading the user.`,
    wrongCredentials: 'Incorrect email or password.',
    loginError: (status) => `Error ${status} signing in.`,
    emailTaken: 'A user with this email already exists.',
    invalidRegistration: 'Fill in all fields. The password must be at least 8 characters long.',
    registerError: (status) => `Error ${status} creating the user.`,
    logoutError: (status) => `Error ${status} signing out.`,
    searchError: (status) => `Error ${status} searching.`,
    artistError: (status) => `Error ${status} loading the artist.`,
    releasesError: (status) => `Error ${status} loading the releases.`,
    settingsError: (status) => `Error ${status} loading the settings.`,
    saveSettingsError: (status) => `Error ${status} saving the settings.`,
  },
  login: {
    email: 'Email',
    name: 'Name',
    password: 'Password',
    signIn: 'Sign in',
    createUser: 'Create account',
    haveUser: 'I already have an account',
    tagline: 'Find the right artist.',
  },
  search: {
    greeting: (name) => `Hi, ${name}`,
    logout: 'Sign out',
    settings: 'Settings',
    artistName: 'Artist name',
    search: 'Search',
    searching: 'Searching…',
    notFound: 'No artist found.',
    error: 'Search failed.',
    hint: "Search by the artist's name.",
  },
  releases: {
    back: 'Back',
    albums: 'Albums',
    eps: 'EPs',
    singles: 'Singles',
    all: 'All',
    loading: 'Loading…',
    empty: 'No official releases found.',
    studio: 'Studio',
    live: 'Live',
    other: 'Other',
    tracks: (count) => (count === 1 ? '1 track' : `${count} tracks`),
    type: 'Release type',
  },
  settings: {
    title: 'Settings',
    edition: 'When an album has several editions, download:',
    original: 'The original',
    originalHint: 'The tracks of the original release.',
    remastered: 'The remaster',
    remasteredHint: 'The most recent remaster.',
    deluxe: 'The deluxe',
    deluxeHint: 'The deluxe edition, with extra tracks.',
    fallback: 'If the album has no such edition, the original is used.',
    loading: 'Loading…',
    saved: 'Saved.',
  },
};

export type Locale = 'pt-BR' | 'en';

const messages: Record<Locale, Messages> = { 'pt-BR': ptBR, en };

export function detectLocale(languages: readonly string[]): Locale {
  const language = languages
    .map((tag) => tag.split('-')[0].toLowerCase())
    .find((base) => base === 'pt' || base === 'en');

  return language === 'pt' ? 'pt-BR' : 'en';
}

export const locale = detectLocale(navigator.languages);

export const t = messages[locale];
