import { AuthenticationProvider, BackingStoreFactory, BackingStoreFactorySingleton, HttpCore as IHttpCore, Parsable, ParseNodeFactory, RequestInformation, ResponseHandler, ParseNodeFactoryRegistry, enableBackingStoreForParseNodeFactory, SerializationWriterFactoryRegistry, enableBackingStoreForSerializationWriterFactory, SerializationWriterFactory } from '@microsoft/kiota-abstractions';
import { ReadableStream } from 'web-streams-polyfill/es2018';
import { URLSearchParams } from 'url';
import { HttpClient } from './httpClient';
import  {Headers as crossHeaders} from "cross-fetch"

import { MiddlewareContext } from './middlewares/middlewareContext';

export class HttpCore implements IHttpCore {
    public getSerializationWriterFactory(): SerializationWriterFactory {
        return this.serializationWriterFactory;
    }
    /**
     * Instantiates a new http core service
     * @param authenticationProvider the authentication provider to use.
     * @param parseNodeFactory the parse node factory to deserialize responses.
     * @param serializationWriterFactory the serialization writer factory to use to serialize request bodies.
     * @param httpClient the http client to use to execute requests.
     */
    public constructor(public readonly authenticationProvider: AuthenticationProvider, private parseNodeFactory: ParseNodeFactory = ParseNodeFactoryRegistry.defaultInstance, private serializationWriterFactory: SerializationWriterFactory = SerializationWriterFactoryRegistry.defaultInstance, private readonly httpClient: HttpClient = new HttpClient()) {
        if (!authenticationProvider) {
            throw new Error('authentication provider cannot be null');
        }
        if (!parseNodeFactory) {
            throw new Error('parse node factory cannot be null');
        }
        if (!serializationWriterFactory) {
            throw new Error('serialization writer factory cannot be null');
        }
        if (!httpClient) {
            throw new Error('http client cannot be null');
        }
    }
    private getResponseContentType = (response: Response): string | undefined => {
        const header = response.headers.get("content-type")?.toLowerCase();
        if (!header) return undefined;
        const segments = header.split(';');
        if (segments.length === 0) return undefined;
        else return segments[0];
    }
    public sendCollectionAsync = async <ModelType extends Parsable>(requestInfo: RequestInformation, type: new () => ModelType, responseHandler: ResponseHandler | undefined): Promise<ModelType[]> => {
        if (!requestInfo) {
            throw new Error('requestInfo cannot be null');
        }
        await this.authenticationProvider.authenticateRequest(requestInfo);

        const response = await this.httpClient.executeFetch(this.createContext(requestInfo));
        if (responseHandler) {
            return await responseHandler.handleResponseAsync(response);
        } else {
            const payload = await response.arrayBuffer();
            const responseContentType = this.getResponseContentType(response);
            if (!responseContentType)
                throw new Error("no response content type found for deserialization");

            const rootNode = this.parseNodeFactory.getRootParseNode(responseContentType, payload);
            const result = rootNode.getCollectionOfObjectValues(type);
            return result as unknown as ModelType[];
        }
    }
    public sendAsync = async <ModelType extends Parsable>(requestInfo: RequestInformation, type: new () => ModelType, responseHandler: ResponseHandler | undefined): Promise<ModelType> => {
        if (!requestInfo) {
            throw new Error('requestInfo cannot be null');
        }
        await this.authenticationProvider.authenticateRequest(requestInfo);

        const response = await this.httpClient.executeFetch(this.createContext(requestInfo));
        if (responseHandler) {
            return await responseHandler.handleResponseAsync(response);
        } else {
            const payload = await response.arrayBuffer();
            const responseContentType = this.getResponseContentType(response);
            if (!responseContentType)
                throw new Error("no response content type found for deserialization");

            const rootNode = this.parseNodeFactory.getRootParseNode(responseContentType, payload);
            const result = rootNode.getObjectValue(type);
            return result as unknown as ModelType;
        }
    }
    public sendPrimitiveAsync = async <ResponseType>(requestInfo: RequestInformation, responseType: "string" | "number" | "boolean" | "Date" | "ReadableStream", responseHandler: ResponseHandler | undefined): Promise<ResponseType> => {
        if (!requestInfo) {
            throw new Error('requestInfo cannot be null');
        }
        await this.authenticationProvider.authenticateRequest(requestInfo);

        const response = await this.httpClient.executeFetch(this.createContext(requestInfo));
        if (responseHandler) {
            return await responseHandler.handleResponseAsync(response);
        } else {
            switch (responseType) {
                case "ReadableStream":
                    const buffer = await response.arrayBuffer();
                    let bufferPulled = false;
                    const stream = new ReadableStream({
                        pull: (controller) => {
                            if (!bufferPulled) {
                                controller.enqueue(buffer.slice(0))
                                bufferPulled = true;
                            }
                        },
                    });
                    return stream as unknown as ResponseType;
                case 'string':
                case 'number':
                case 'boolean':
                case 'Date':
                    const payload = await response.arrayBuffer();
                    const responseContentType = this.getResponseContentType(response);
                    if (!responseContentType)
                        throw new Error("no response content type found for deserialization");

                    const rootNode = this.parseNodeFactory.getRootParseNode(responseContentType, payload);
                    if (responseType === 'string') {
                        return rootNode.getStringValue() as unknown as ResponseType;
                    } else if (responseType === 'number') {
                        return rootNode.getNumberValue() as unknown as ResponseType;
                    } else if (responseType === 'boolean') {
                        return rootNode.getBooleanValue() as unknown as ResponseType;
                    } else if (responseType === 'Date') {
                        return rootNode.getDateValue() as unknown as ResponseType;
                    } else {
                        throw new Error("unexpected type to deserialize");
                    }
            }
        }
    }
    public sendNoResponseContentAsync = async (requestInfo: RequestInformation, responseHandler: ResponseHandler | undefined): Promise<void> => {
        if (!requestInfo) {
            throw new Error('requestInfo cannot be null');
        }
        await this.authenticationProvider.authenticateRequest(requestInfo);
        const response = await this.httpClient.executeFetch(this.createContext(requestInfo));
        if (responseHandler) {
            return await responseHandler.handleResponseAsync(response);
        }
    }
    public enableBackingStore = (backingStoreFactory?: BackingStoreFactory | undefined): void => {
        this.parseNodeFactory = enableBackingStoreForParseNodeFactory(this.parseNodeFactory);
        this.serializationWriterFactory = enableBackingStoreForSerializationWriterFactory(this.serializationWriterFactory);
        if (!this.serializationWriterFactory || !this.parseNodeFactory)
            throw new Error("unable to enable backing store");
        if (backingStoreFactory) {
            BackingStoreFactorySingleton.instance = backingStoreFactory;
        }
    }
    private getRequestFromRequestInformation = (requestInfo: RequestInformation): RequestInit => {
        const request = {
            method: requestInfo.httpMethod?.toString(),
            headers: new crossHeaders(),
            body: requestInfo.content,
        } as RequestInit;

        requestInfo.headers?.forEach((v, k) => (request.headers as Headers).set(k, v));

        requestInfo.options?.forEach((v, k) => {
            if (k in request) {
                request[k] = v;
            }
        }
        );
        return request;
    }
    private getRequestUrl (requestInfo: RequestInformation): string {
        let url = requestInfo.URI ?? '';
        if (requestInfo.queryParameters?.size ?? -1 > 0) {
            const queryParametersBuilder = new URLSearchParams();
            requestInfo.queryParameters?.forEach((v, k) => {
                queryParametersBuilder.append(k, `${v}`);
            });
            url = url + '?' + queryParametersBuilder.toString();
        }
        return url;
    }

    private createContext(requestInformation: RequestInformation): MiddlewareContext {

        const url =  this.getRequestUrl(requestInformation);
        const context: MiddlewareContext = {
            request: url,
            options: this.getRequestFromRequestInformation(requestInformation),
            //middlewareControl //set from middleware options
        }
        return context;

    }
}
