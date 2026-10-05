import { useEffect, useState } from 'react';
import { api, setUnauthorizedHandler } from './api';
import { BoardsPage } from './pages/BoardsPage';
import { ComponentsPage } from './pages/ComponentsPage';
import { LoginPage } from './pages/LoginPage';
import { OrdersPage } from './pages/OrdersPage';

const tabs = {
  orders: { label: 'Orders', page: OrdersPage },
  boards: { label: 'Boards', page: BoardsPage },
  components: { label: 'Components', page: ComponentsPage },
} as const;

type Tab = keyof typeof tabs;

/**
 * Shows the login page until the API confirms a session, then the three list pages as tabs.
 * Tabs are plain component state; there is no router.
 */
export function App() {
  // undefined while the session is being checked, null when nobody is logged in.
  const [user, setUser] = useState<string | null | undefined>(undefined);
  const [tab, setTab] = useState<Tab>('orders');

  useEffect(() => {
    setUnauthorizedHandler(() => setUser(null));
    void api.me().then((result) => setUser(result.ok ? result.value.username : null));
  }, []);

  if (user === undefined) {
    return null;
  }

  if (user === null) {
    return <LoginPage onLoggedIn={setUser} />;
  }

  const Page = tabs[tab].page;

  return (
    <div className="app">
      <header>
        <h1>SMT Order Manager</h1>
        <nav>
          {(Object.keys(tabs) as Tab[]).map((key) => (
            <button key={key} type="button" className={key === tab ? 'active' : undefined} onClick={() => setTab(key)}>
              {tabs[key].label}
            </button>
          ))}
        </nav>
        <div className="user">
          {user}
          <button type="button" onClick={() => void api.logout().then(() => setUser(null))}>
            Log out
          </button>
        </div>
      </header>
      <main>
        <Page key={tab} />
      </main>
    </div>
  );
}
