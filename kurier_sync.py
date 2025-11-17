import requests
from requests.auth import HTTPBasicAuth

# Configurações
KURIER_USER = 'seu_usuario'
KURIER_PASS = 'sua_senha'
KURIER_BASE = 'https://www.kurierservicos.com.br/wsservicos'
BENNER_URL = 'https://meu-benner/api/integracao'

def sync_publicacoes():
    try:
        r = requests.get(
            f'{KURIER_BASE}/api/KJuridico/ConsultarPublicacoes',
            auth=HTTPBasicAuth(KURIER_USER, KURIER_PASS),
            timeout=60
        )
        r.raise_for_status()
        publicacoes = r.json()
        print('Publicações:', publicacoes)
    except Exception as e:
        print('Erro ao consultar publicações:', e)
        return

    for pub in publicacoes:
        try:
            resp = requests.post(BENNER_URL, json=pub, timeout=30)
            print('Enviado para Benner:', resp.status_code)
        except Exception as e:
            print('Erro ao enviar para Benner:', e)
            continue

        try:
            conf = requests.post(
                f'{KURIER_BASE}/api/KJuridico/ConfirmarPublicacoes',
                auth=HTTPBasicAuth(KURIER_USER, KURIER_PASS),
                json=[pub.get('id')], # ou ajuste conforme campo identificador
                timeout=30
            )
            print('Confirmação Kurier:', conf.status_code)
        except Exception as e:
            print('Erro ao confirmar publicação:', e)

def sync_distribuicoes():
    try:
        r = requests.get(
            f'{KURIER_BASE}/api/KDistribuicao/ConsultarDistribuicoes',
            auth=HTTPBasicAuth(KURIER_USER, KURIER_PASS),
            timeout=60
        )
        r.raise_for_status()
        distribuicoes = r.json()
        print('Distribuições:', distribuicoes)
    except Exception as e:
        print('Erro ao consultar distribuições:', e)
        return

    for dist in distribuicoes:
        try:
            resp = requests.post(BENNER_URL, json=dist, timeout=30)
            print('Enviado para Benner:', resp.status_code)
        except Exception as e:
            print('Erro ao enviar distribuição:', e)
            continue

        try:
            conf = requests.post(
                f'{KURIER_BASE}/api/KDistribuicao/ConfirmarDistribuicoes',
                auth=HTTPBasicAuth(KURIER_USER, KURIER_PASS),
                json=[dist.get('id')], # ou ajuste conforme campo identificador
                timeout=30
            )
            print('Confirmação Kurier:', conf.status_code)
        except Exception as e:
            print('Erro ao confirmar distribuição:', e)

if __name__ == '__main__':
    sync_publicacoes()
    sync_distribuicoes()
