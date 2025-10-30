import os
import sys
import requests
from dotenv import load_dotenv


def get_env():
    load_dotenv()
    env = {
        'KJ_USER': os.getenv('KJ_USER'),
        'KJ_PASS': os.getenv('KJ_PASS'),
        'KD_USER': os.getenv('KD_USER'),
        'KD_PASS': os.getenv('KD_PASS'),
        'BENNER_URL': 'https://seguro-pjur.transpetro.com.br/api/v1/jur/pastas/documento',
        'BENNER_TOKEN': os.getenv('BENNER_TOKEN')
    }
    missing = [k for k, v in env.items() if not v]
    if missing:
        print(f"Faltam variáveis no .env: {', '.join(missing)}")
        sys.exit(1)
    return env


def kj_consultar(user, pwd):
    url = (
        'https://www.kurierservicos.com.br/wsservicos/api/KJuridico/'
        'ConsultarPublicacoes'
    )
    try:
        r = requests.get(url, auth=(user, pwd), timeout=30)
        r.raise_for_status()
        data = r.json()
        items = data if isinstance(data, list) else data.get('Publicacoes', [])
        print(f"KJ: {len(items)} publicações")
        return items
    except Exception as e:
        print(f"KJ erro: {e}")
        return []


def kj_confirmar(user, pwd, ids):
    if not ids:
        return
    url = (
        'https://www.kurierservicos.com.br/wsservicos/api/KJuridico/'
        'ConfirmarPublicacoes'
    )
    payload = {'Ids': ids}
    try:
        r = requests.post(url, json=payload, auth=(user, pwd), timeout=30)
        r.raise_for_status()
    except Exception as e:
        print(f"KJ confirmação erro: {e}")


def kd_consultar(user, pwd):
    url = (
        'https://www.kurierservicos.com.br/wsservicos/api/KDistribuicao/'
        'ConsultarDistribuicoes'
    )
    try:
        r = requests.get(url, auth=(user, pwd), timeout=30)
        if r.status_code == 401:
            print("KD erro 401: verifique KD_USER/KD_PASS no .env")
            return []
        r.raise_for_status()
        data = r.json()
        items = (
            data if isinstance(data, list)
            else data.get('Distribuicoes', [])
        )
        print(f"KD: {len(items)} distribuições")
        return items
    except Exception as e:
        print(f"KD erro: {e}")
        return []


def kd_confirmar(user, pwd, numero):
    if not numero:
        return
    url = (
        'https://www.kurierservicos.com.br/wsservicos/api/KDistribuicao/'
        'ConfirmarDistribuicoes'
    )
    payload = {'NumeroProcesso': numero}
    try:
        r = requests.post(url, json=payload, auth=(user, pwd), timeout=30)
        r.raise_for_status()
    except Exception as e:
        print(f"KD confirmação erro: {e}")


def enviar_benner(url, token, item):
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json"
    }
    # Função de depuração: testar vários payloads
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
        "Accept": "application/json"
    }
    payload = {
        "descricao": "Teste integração Benner",
        "ativo": True,
        "handlePasta": 1,
        "tipoDocumento": "Publicacao",
        "dataDocumento": "2025-10-27T00:00:00"
    }
    try:
        r = requests.post(url, json=payload, headers=headers, timeout=30)
        print(f"Status: {r.status_code}")
        print(f"Headers: {r.headers}")
        print(f"Text: {r.text}")
        print(f"Request body: {r.request.body}")
        print(f"URL: {r.url}")
        if r.status_code >= 400:
            with open('log.txt', 'w', encoding='utf-8') as f:
                f.write(r.text)
            print("Erro completo salvo em log.txt")
    except Exception as e:
        print(f"Benner erro: {e}")


def main():
    env = get_env()
    pubs = kj_consultar(env['KJ_USER'], env['KJ_PASS'])
    for item in pubs:
        r = requests.get(
            'https://www.kurierservicos.com.br/wsservicos/api/KJuridico/'
            'ConsultarPublicacoes',
            auth=(env['KJ_USER'], env['KJ_PASS']),
            timeout=30
        )
        if r.status_code == 200:
            print("KJ OK")
        enviar_benner(env['BENNER_URL'], env['BENNER_TOKEN'], item)
    dists = kd_consultar(env['KD_USER'], env['KD_PASS'])
    for item in dists:
        r = requests.get(
            'https://www.kurierservicos.com.br/wsservicos/api/KDistribuicao/'
            'ConsultarDistribuicoes',
            auth=(env['KD_USER'], env['KD_PASS']),
            timeout=30
        )
        if r.status_code == 200:
            print("KD OK")
        enviar_benner(env['BENNER_URL'], env['BENNER_TOKEN'], item)


if __name__ == '__main__':
    main()
